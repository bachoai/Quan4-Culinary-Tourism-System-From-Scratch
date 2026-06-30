using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Helpers;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class AudioService
{
    private readonly PoiRepository _poiRepository;
    private readonly PoiLocalizationRepository _poiLocalizationRepository;
    private readonly PoiAudioRepository _poiAudioRepository;
    private readonly FileUploadHelper _fileUploadHelper;
    private readonly PythonTextToSpeechService _pythonTextToSpeechService;
    private readonly ILogger<AudioService> _logger;

    public AudioService(
        PoiRepository poiRepository,
        PoiLocalizationRepository poiLocalizationRepository,
        PoiAudioRepository poiAudioRepository,
        FileUploadHelper fileUploadHelper,
        PythonTextToSpeechService pythonTextToSpeechService,
        ILogger<AudioService> logger)
    {
        _poiRepository = poiRepository;
        _poiLocalizationRepository = poiLocalizationRepository;
        _poiAudioRepository = poiAudioRepository;
        _fileUploadHelper = fileUploadHelper;
        _pythonTextToSpeechService = pythonTextToSpeechService;
        _logger = logger;
    }

    public Task<List<AudioLanguageResponse>> GetLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var languages = SharedConstants.SupportedLanguages.Select(lang => new AudioLanguageResponse { Code = lang, Name = lang }).ToList();
        return Task.FromResult(languages);
    }

    public async Task<PoiAudioResponse?> GetPoiAudioAsync(string poiId, string? lang, CancellationToken cancellationToken = default)
    {
        var normalizedLang = NormalizeLanguage(lang);
        var poi = await _poiRepository.GetByIdAsync(poiId, cancellationToken);
        if (poi is null)
        {
            return null;
        }

        var localization = normalizedLang == "vi"
            ? null
            : await _poiLocalizationRepository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken);
        var narrationText = ResolveNarrationText(normalizedLang, poi, localization);
        var narrationSignature = ComputeNarrationSignature(normalizedLang, narrationText);
        var audio = await _poiAudioRepository.GetByPoiAndLangAsync(
            poiId,
            normalizedLang,
            cancellationToken,
            includeDeleted: true);

        if (audio?.IsDeleted == true)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                var fallbackAudio = (await _poiAudioRepository.GetByPoiIdAsync(poiId, cancellationToken)).FirstOrDefault();
                return fallbackAudio is null ? null : ToResponse(fallbackAudio);
            }

            return null;
        }

        if (ShouldRegenerateGeneratedAudio(audio, narrationSignature))
        {
            var regenerated = await TryGenerateNarrationAudioAsync(
                poi,
                normalizedLang,
                narrationText,
                narrationSignature,
                cancellationToken);
            if (regenerated is not null)
            {
                return regenerated;
            }
        }

        if (audio is null)
        {
            var generated = await TryGenerateNarrationAudioAsync(
                poi,
                normalizedLang,
                narrationText,
                narrationSignature,
                cancellationToken);
            if (generated is not null)
            {
                return generated;
            }
        }

        if (string.IsNullOrWhiteSpace(lang))
        {
            audio ??= (await _poiAudioRepository.GetByPoiIdAsync(poiId, cancellationToken)).FirstOrDefault();
        }

        return audio is null ? null : ToResponse(audio);
    }

    public async Task<PoiAudioResponse> UploadOrSetAudioAsync(string poiId, UploadPoiAudioRequest request, IFormFile? file, CancellationToken cancellationToken = default)
    {
        var poi = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);

        string audioUrl;
        long fileSize = 0;
        string storageProvider = "external";
        string? objectKey = null;
        string? resourceType = null;
        if (file is not null)
        {
            _fileUploadHelper.ValidateAudio(file);
            var storedFile = await _fileUploadHelper.SaveFileAsync(file, "audio", cancellationToken);
            audioUrl = storedFile.Url;
            fileSize = storedFile.SizeBytes;
            storageProvider = storedFile.StorageProvider;
            objectKey = storedFile.ObjectKey;
            resourceType = storedFile.ResourceType;
        }
        else if (!string.IsNullOrWhiteSpace(request.AudioUrl))
        {
            audioUrl = request.AudioUrl;
        }
        else
        {
            throw new ApiException("Cần upload file hoặc truyền AudioUrl.");
        }

        var audio = new PoiAudio
        {
            PoiId = poi.Id,
            Lang = request.Lang,
            IsDeleted = false,
            AudioUrl = audioUrl,
            StorageProvider = storageProvider,
            ObjectKey = objectKey,
            ResourceType = resourceType,
            VoiceName = request.VoiceName,
            SourceType = request.SourceType,
            Status = SharedConstants.AudioDone,
            FileSizeBytes = fileSize
        };

        await _poiAudioRepository.UpsertAsync(audio, cancellationToken);
        poi.AudioStatus = SharedConstants.AudioDone;
        await _poiRepository.UpdateAsync(poi, cancellationToken);
        return ToResponse(audio);
    }

    public async Task<PoiAudioResponse> GeneratePoiAudioAsync(
        string poiId,
        GeneratePoiAudioRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedLang = NormalizeLanguage(request.Lang);
        var poi = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);

        var localization = normalizedLang == "vi"
            ? null
            : await _poiLocalizationRepository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken);
        var narrationText = ResolveNarrationText(normalizedLang, poi, localization);
        if (string.IsNullOrWhiteSpace(narrationText))
        {
            throw new ApiException("Không có nội dung lời nói để tạo audio cho ngôn ngữ đã chọn.");
        }

        var generated = await _pythonTextToSpeechService.GenerateAudioAsync(
            narrationText,
            ResolveVoiceHint(normalizedLang, request.VoiceName),
            cancellationToken);
        if (generated is null)
        {
            throw new ApiException(
                "Không thể tạo audio từ nội dung lời nói. Hãy kiểm tra cấu hình TTS của máy chủ.",
                StatusCodes.Status500InternalServerError);
        }

        var narrationSignature = ComputeNarrationSignature(normalizedLang, narrationText);
        return await SaveGeneratedNarrationAudioAsync(
            poi,
            normalizedLang,
            narrationSignature,
            generated,
            cancellationToken);
    }

    public async Task DeletePoiAudioAsync(string poiId, string? lang, CancellationToken cancellationToken = default)
    {
        var normalizedLang = NormalizeLanguage(lang);
        var poi = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("KhĂ´ng tĂ¬m tháº¥y POI.", StatusCodes.Status404NotFound);
        var audio = await _poiAudioRepository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken)
            ?? throw new ApiException("KhĂ´ng tĂ¬m tháº¥y audio cho ngĂ´n ngá»¯ Ä‘Ă£ chá»n.", StatusCodes.Status404NotFound);

        await _fileUploadHelper.DeleteManagedFileAsync(
            audio.AudioUrl,
            audio.StorageProvider,
            audio.ObjectKey,
            audio.ResourceType,
            cancellationToken);
        audio.IsDeleted = true;
        audio.AudioUrl = string.Empty;
        audio.StorageProvider = "external";
        audio.ObjectKey = null;
        audio.ResourceType = null;
        audio.FileSizeBytes = 0;
        audio.DurationSeconds = 0;
        audio.VoiceName = null;
        audio.NarrationSignature = null;
        audio.Status = SharedConstants.AudioPending;
        await _poiAudioRepository.UpsertAsync(audio, cancellationToken);
        await SyncPoiAudioStatusAsync(poi, cancellationToken);
    }

    public async Task<object> GetPackManifestAsync(CancellationToken cancellationToken = default)
    {
        var pois = await _poiRepository.GetPublicPoisAsync(cancellationToken);
        var items = new List<object>();
        foreach (var poi in pois)
        {
            var audios = await _poiAudioRepository.GetByPoiIdAsync(poi.Id, cancellationToken);
            items.Add(new
            {
                poiId = poi.Id,
                poiName = poi.Name,
                audios = audios.Select(a => new { a.Lang, a.AudioUrl, a.Status })
            });
        }

        return new
        {
            version = "v1",
            generatedAt = DateTime.UtcNow,
            items
        };
    }

    private static PoiAudioResponse ToResponse(PoiAudio audio) => new()
    {
        Id = audio.Id,
        PoiId = audio.PoiId,
        Lang = audio.Lang,
        AudioUrl = audio.AudioUrl,
        VoiceName = audio.VoiceName,
        SourceType = audio.SourceType,
        Status = audio.Status,
        DurationSeconds = audio.DurationSeconds,
        FileSizeBytes = audio.FileSizeBytes
    };

    private async Task<PoiAudioResponse?> TryGenerateNarrationAudioAsync(
        Poi poi,
        string lang,
        string? narrationText,
        string? narrationSignature,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(narrationText))
            {
                return null;
            }

            var generated = await _pythonTextToSpeechService.GenerateAudioAsync(narrationText, lang, cancellationToken);
            if (generated is null)
            {
                return null;
            }

            return await SaveGeneratedNarrationAudioAsync(
                poi,
                lang,
                narrationSignature,
                generated,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to auto-generate narration audio for POI {PoiId}", poi.Id);
            return null;
        }
    }

    private async Task<PoiAudioResponse> SaveGeneratedNarrationAudioAsync(
        Poi poi,
        string lang,
        string? narrationSignature,
        GeneratedAudioResult generated,
        CancellationToken cancellationToken)
    {
        var audio = new PoiAudio
        {
            PoiId = poi.Id,
            Lang = lang,
            IsDeleted = false,
            AudioUrl = generated.PublicUrl,
            StorageProvider = generated.StorageProvider,
            ObjectKey = generated.ObjectKey,
            ResourceType = generated.ResourceType,
            VoiceName = generated.VoiceName,
            SourceType = "python_tts",
            NarrationSignature = narrationSignature,
            Status = SharedConstants.AudioDone,
            FileSizeBytes = generated.FileSizeBytes
        };

        await _poiAudioRepository.UpsertAsync(audio, cancellationToken);
        await SyncPoiAudioStatusAsync(poi, cancellationToken);
        return ToResponse(audio);
    }

    private async Task SyncPoiAudioStatusAsync(Poi poi, CancellationToken cancellationToken)
    {
        var remainingAudios = await _poiAudioRepository.GetByPoiIdAsync(poi.Id, cancellationToken);
        poi.AudioStatus = remainingAudios.Count > 0
            ? SharedConstants.AudioDone
            : SharedConstants.AudioPending;
        await _poiRepository.UpdateAsync(poi, cancellationToken);
    }

    private static string NormalizeLanguage(string? lang)
    {
        var normalized = string.IsNullOrWhiteSpace(lang) ? "vi" : lang.Trim().ToLowerInvariant();
        return SharedConstants.SupportedLanguages.Contains(normalized) ? normalized : "vi";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? ResolveNarrationText(string lang, Poi poi, PoiLocalization? localization)
    {
        if (lang == "vi")
        {
            return FirstNonEmpty(
                poi.TtsScript,
                poi.Description);
        }

        return FirstNonEmpty(
            localization?.TtsScript,
            localization?.Description);
    }

    private static string ResolveVoiceHint(string lang, string? voiceName) =>
        string.IsNullOrWhiteSpace(voiceName) ? lang : voiceName.Trim();

    private static bool ShouldRegenerateGeneratedAudio(PoiAudio? audio, string? narrationSignature)
    {
        if (audio is null || audio.IsDeleted || !string.Equals(audio.SourceType, "python_tts", StringComparison.Ordinal))
        {
            return false;
        }

        return !string.Equals(audio.NarrationSignature, narrationSignature, StringComparison.Ordinal);
    }

    private static string? ComputeNarrationSignature(string lang, string? narrationText)
    {
        if (string.IsNullOrWhiteSpace(narrationText))
        {
            return null;
        }

        var payload = $"{lang}:{narrationText.Trim()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
