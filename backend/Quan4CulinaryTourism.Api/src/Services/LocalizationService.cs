using Microsoft.AspNetCore.Http;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class LocalizationService
{
    private readonly PoiRepository _poiRepository;
    private readonly PoiLocalizationRepository _repository;
    private readonly PythonTranslationService _pythonTranslationService;
    private readonly ILogger<LocalizationService> _logger;

    public LocalizationService(
        PoiRepository poiRepository,
        PoiLocalizationRepository repository,
        PythonTranslationService pythonTranslationService,
        ILogger<LocalizationService> logger)
    {
        _poiRepository = poiRepository;
        _repository = repository;
        _pythonTranslationService = pythonTranslationService;
        _logger = logger;
    }

    public bool CanAutoTranslate() => _pythonTranslationService.IsAvailable();

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
        if (!CanAutoTranslate())
        {
            throw new ApiException(
                "Auto-translate chua san sang. Can Python translation runtime tren backend.",
                StatusCodes.Status503ServiceUnavailable);
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
        var translated = await _pythonTranslationService.TranslateAsync(sourceLang, targetLang, sourcePayload, cancellationToken)
            ?? throw new ApiException(
                "Khong the tu dong dich localization. Hay kiem tra Python translation tren backend.",
                StatusCodes.Status502BadGateway);

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

    public async Task<PoiLocalization?> EnsureLocalizationAsync(
        string poiId,
        string lang,
        CancellationToken cancellationToken = default)
    {
        var normalizedLang = NormalizeLanguage(lang);
        if (string.Equals(normalizedLang, SharedConstants.DefaultAudioLanguage, StringComparison.Ordinal))
        {
            return null;
        }

        var existing = await _repository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (!CanAutoTranslate())
        {
            return null;
        }

        try
        {
            await TranslateAsync(
                poiId,
                new TranslatePoiLocalizationRequest
                {
                    Lang = normalizedLang,
                    SourceLang = SharedConstants.DefaultAudioLanguage,
                    OverwriteExisting = false
                },
                cancellationToken);

            return await _repository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken);
        }
        catch (Exception exception)
        {
            var createdByAnotherRequest = await _repository.GetByPoiAndLangAsync(poiId, normalizedLang, cancellationToken);
            if (createdByAnotherRequest is not null)
            {
                return createdByAnotherRequest;
            }

            _logger.LogWarning(
                exception,
                "Unable to auto-create localization for POI {PoiId} in {Lang}",
                poiId,
                normalizedLang);
            return null;
        }
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

    private static string NormalizeLanguage(string lang)
    {
        var normalized = string.IsNullOrWhiteSpace(lang) ? SharedConstants.DefaultAudioLanguage : lang.Trim().ToLowerInvariant();
        return SharedConstants.SupportedLanguages.Contains(normalized) ? normalized : SharedConstants.DefaultAudioLanguage;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
