using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class LocalizationService
{
    private static readonly JsonSerializerOptions TranslationJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PoiRepository _poiRepository;
    private readonly PoiLocalizationRepository _repository;
    private readonly AiChatClient _aiChatClient;

    public LocalizationService(PoiRepository poiRepository, PoiLocalizationRepository repository, AiChatClient aiChatClient)
    {
        _poiRepository = poiRepository;
        _repository = repository;
        _aiChatClient = aiChatClient;
    }

    public async Task<List<PoiLocalizationResponse>> GetPoiLocalizationsAsync(string poiId, CancellationToken cancellationToken = default)
    {
        _ = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);
        return (await _repository.GetByPoiIdAsync(poiId, cancellationToken)).Select(ToResponse).ToList();
    }

    public async Task<PoiLocalizationResponse> CreateAsync(string poiId, CreatePoiLocalizationRequest request, CancellationToken cancellationToken = default)
    {
        _ = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);
        var normalizedLang = NormalizeLanguage(request.Lang);
        var existing = await _repository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken);
        if (existing is not null)
        {
            throw new ApiException("Localization đã tồn tại.");
        }

        var entity = new PoiLocalization
        {
            PoiId = poiId,
            Lang = normalizedLang,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            AudioUrl = request.AudioUrl,
            TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim(),
            IsFallback = request.IsFallback
        };
        await _repository.CreateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public async Task<PoiLocalizationResponse> UpsertAsync(string poiId, CreatePoiLocalizationRequest request, CancellationToken cancellationToken = default)
    {
        _ = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);

        var normalizedLang = NormalizeLanguage(request.Lang);
        var existing = await _repository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken);
        if (existing is null)
        {
            var created = new PoiLocalization
            {
                PoiId = poiId,
                Lang = normalizedLang,
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                AudioUrl = request.AudioUrl,
                TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim(),
                IsFallback = request.IsFallback
            };
            await _repository.CreateAsync(created, cancellationToken);
            return ToResponse(created);
        }

        existing.Lang = normalizedLang;
        existing.Name = request.Name.Trim();
        existing.Description = request.Description.Trim();
        existing.AudioUrl = request.AudioUrl;
        existing.TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim();
        existing.IsFallback = request.IsFallback;
        await _repository.UpdateAsync(existing, cancellationToken);
        return ToResponse(existing);
    }

    public async Task<PoiLocalizationResponse> TranslateAsync(
        string poiId,
        TranslatePoiLocalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_aiChatClient.CanUseAi())
        {
            throw new ApiException("AI translation chua duoc cau hinh.", StatusCodes.Status503ServiceUnavailable);
        }

        var poi = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);

        var sourceLang = NormalizeLanguage(request.SourceLang);
        var targetLang = NormalizeLanguage(request.Lang);
        if (string.Equals(sourceLang, targetLang, StringComparison.Ordinal))
        {
            throw new ApiException("Ngon ngu dich phai khac ngon ngu nguon.");
        }

        var existing = await _repository.GetByPoiAndLangAsync(poiId, targetLang, cancellationToken);
        if (existing is not null && !request.OverwriteExisting)
        {
            return ToResponse(existing);
        }

        var sourcePayload = await LoadSourcePayloadAsync(poi, sourceLang, cancellationToken);
        var translated = await TranslatePayloadAsync(sourceLang, targetLang, sourcePayload, cancellationToken)
            ?? throw new ApiException("AI translation khong tra ve du lieu hop le.", StatusCodes.Status502BadGateway);

        return await UpsertAsync(
            poiId,
            new CreatePoiLocalizationRequest
            {
                Lang = targetLang,
                Name = translated.Name,
                Description = translated.Description,
                TtsScript = string.IsNullOrWhiteSpace(translated.TtsScript) ? translated.Description : translated.TtsScript,
                AudioUrl = existing?.AudioUrl,
                IsFallback = existing?.IsFallback ?? false
            },
            cancellationToken);
    }

    public async Task<PoiLocalizationResponse> UpdateAsync(string poiId, string lang, UpdatePoiLocalizationRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedLang = NormalizeLanguage(lang);
        var entity = await _repository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken)
            ?? throw new ApiException("Không tìm thấy localization.", StatusCodes.Status404NotFound);
        entity.Lang = normalizedLang;
        entity.Name = request.Name.Trim();
        entity.Description = request.Description.Trim();
        entity.AudioUrl = request.AudioUrl;
        entity.TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim();
        entity.IsFallback = request.IsFallback;
        await _repository.UpdateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public async Task DeleteAsync(string poiId, string lang, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByPoiAndLangAsync(poiId, lang, cancellationToken)
            ?? throw new ApiException("Không tìm thấy localization.", StatusCodes.Status404NotFound);
        await _repository.DeleteAsync(entity.PoiId, entity.Lang, cancellationToken);
    }

    private static PoiLocalizationResponse ToResponse(PoiLocalization entity) => new()
    {
        Id = entity.Id,
        PoiId = entity.PoiId,
        Lang = entity.Lang,
        Name = entity.Name,
        Description = entity.Description,
        AudioUrl = entity.AudioUrl,
        TtsScript = entity.TtsScript,
        IsFallback = entity.IsFallback
    };

    private async Task<LocalizationTranslationPayload> LoadSourcePayloadAsync(Poi poi, string sourceLang, CancellationToken cancellationToken)
    {
        if (sourceLang == "vi")
        {
            return new LocalizationTranslationPayload
            {
                Name = poi.Name.Trim(),
                Description = poi.Description.Trim(),
                TtsScript = FirstNonEmpty(poi.TtsScript, poi.Description) ?? string.Empty
            };
        }

        var localization = await _repository.GetByPoiAndLangAsync(poi.Id, sourceLang, cancellationToken)
            ?? throw new ApiException("Khong tim thay localization nguon.", StatusCodes.Status404NotFound);

        return new LocalizationTranslationPayload
        {
            Name = localization.Name.Trim(),
            Description = localization.Description.Trim(),
            TtsScript = FirstNonEmpty(localization.TtsScript, localization.Description) ?? string.Empty
        };
    }

    private async Task<LocalizationTranslationPayload?> TranslatePayloadAsync(
        string sourceLang,
        string targetLang,
        LocalizationTranslationPayload sourcePayload,
        CancellationToken cancellationToken)
    {
        var rawResponse = await _aiChatClient.GenerateReplyAsync(
            BuildTranslationSystemPrompt(targetLang),
            BuildTranslationUserPrompt(sourceLang, targetLang, sourcePayload),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        var json = ExtractJsonObject(rawResponse);
        if (json is null)
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<LocalizationTranslationPayload>(json, TranslationJsonOptions);
            if (payload is null)
            {
                return null;
            }

            payload.Name = payload.Name?.Trim() ?? string.Empty;
            payload.Description = payload.Description?.Trim() ?? string.Empty;
            payload.TtsScript = payload.TtsScript?.Trim();
            if (string.IsNullOrWhiteSpace(payload.Name) || string.IsNullOrWhiteSpace(payload.Description))
            {
                return null;
            }

            return payload;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildTranslationSystemPrompt(string targetLang) =>
        $"""
        You translate POI content for a culinary tourism app.
        Return strict JSON only with keys: name, description, ttsScript.
        Translate into {targetLang}.
        Keep the meaning accurate, keep proper nouns natural, and do not add markdown.
        The description should read naturally for an app detail page.
        The ttsScript should read naturally as spoken narration in the target language.
        """;

    private static string BuildTranslationUserPrompt(
        string sourceLang,
        string targetLang,
        LocalizationTranslationPayload sourcePayload) =>
        $$"""
        Source language: {{sourceLang}}
        Target language: {{targetLang}}

        Translate this JSON payload and return JSON only:
        {{JsonSerializer.Serialize(sourcePayload, TranslationJsonOptions)}}
        """;

    private static string NormalizeLanguage(string lang)
    {
        var normalized = string.IsNullOrWhiteSpace(lang) ? "vi" : lang.Trim().ToLowerInvariant();
        return SharedConstants.SupportedLanguages.Contains(normalized) ? normalized : "vi";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? ExtractJsonObject(string rawResponse)
    {
        var start = rawResponse.IndexOf('{');
        var end = rawResponse.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return rawResponse[start..(end + 1)];
    }

    private sealed class LocalizationTranslationPayload
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? TtsScript { get; set; }
    }
}
