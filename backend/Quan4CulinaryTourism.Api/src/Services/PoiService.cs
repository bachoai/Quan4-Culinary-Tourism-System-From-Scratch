using Microsoft.AspNetCore.Http;
using MongoDB.Driver.GeoJsonObjectModel;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Helpers;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class PoiService
{
    private readonly PoiRepository _poiRepository;
    private readonly PoiLocalizationRepository _localizationRepository;
    private readonly DistanceHelper _distanceHelper;
    private readonly LocalizationService _localizationService;
    private readonly ILogger<PoiService> _logger;

    public PoiService(
        PoiRepository poiRepository,
        PoiLocalizationRepository localizationRepository,
        DistanceHelper distanceHelper,
        LocalizationService localizationService,
        ILogger<PoiService> logger)
    {
        _poiRepository = poiRepository;
        _localizationRepository = localizationRepository;
        _distanceHelper = distanceHelper;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task<List<PoiResponse>> LoadAllAsync(PoiSearchRequest request, CancellationToken cancellationToken = default)
    {
        var pois = await _poiRepository.GetPublicFilteredAsync(request, cancellationToken);
        return await MapManyAsync(pois, request.Lang, request.AudioLang, cancellationToken);
    }

    public async Task<PoiDetailResponse> GetByIdAsync(
        string id,
        string? lang = null,
        string? audioLang = null,
        CancellationToken cancellationToken = default)
    {
        var poi = await _poiRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Khong tim thay POI.", StatusCodes.Status404NotFound);
        return await MapDetailAsync(poi, lang, audioLang, cancellationToken);
    }

    public async Task<List<NearbyPoiResponse>> NearbyAsync(
        double lat,
        double lng,
        int radius,
        int limit,
        string? lang,
        string? audioLang,
        CancellationToken cancellationToken = default)
    {
        if (lat is < -90 or > 90)
        {
            throw new ApiException("Latitude phai nam trong khoang -90 den 90.");
        }

        if (lng is < -180 or > 180)
        {
            throw new ApiException("Longitude phai nam trong khoang -180 den 180.");
        }

        if (radius is <= 0 or > 10000)
        {
            throw new ApiException("Radius phai tu 1 den 10000 met.");
        }

        var pois = await _poiRepository.GetPublicPoisAsync(cancellationToken);
        var nearby = new List<NearbyPoiResponse>();
        foreach (var poi in pois)
        {
            var poiLng = poi.Location.Coordinates.Longitude;
            var poiLat = poi.Location.Coordinates.Latitude;
            var distance = _distanceHelper.CalculateDistanceMeters(lat, lng, poiLat, poiLng);
            if (distance > radius)
            {
                continue;
            }

            var mapped = await MapAsync(poi, lang, audioLang, cancellationToken);
            nearby.Add(new NearbyPoiResponse
            {
                Id = mapped.Id,
                Name = mapped.Name,
                Description = mapped.Description,
                CategoryId = mapped.CategoryId,
                Address = mapped.Address,
                Ward = mapped.Ward,
                District = mapped.District,
                City = mapped.City,
                PriceRange = mapped.PriceRange,
                Rating = mapped.Rating,
                ReviewCount = mapped.ReviewCount,
                Priority = mapped.Priority,
                MapUrl = mapped.MapUrl,
                TtsScript = mapped.TtsScript,
                Latitude = mapped.Latitude,
                Longitude = mapped.Longitude,
                GeofenceRadiusMeters = mapped.GeofenceRadiusMeters,
                AutoNarrationEnabled = mapped.AutoNarrationEnabled,
                Tags = mapped.Tags,
                Images = mapped.Images,
                IsActive = mapped.IsActive,
                DistanceMeters = Math.Round(distance, 2)
            });
        }

        return nearby.OrderBy(x => x.DistanceMeters).Take(Math.Clamp(limit, 1, 100)).ToList();
    }

    public async Task<List<PoiResponse>> SearchAsync(PoiSearchRequest request, bool publicOnly = true, CancellationToken cancellationToken = default)
    {
        var pois = await _poiRepository.SearchAsync(request, publicOnly, cancellationToken);
        return await MapManyAsync(pois, request.Lang, request.AudioLang, cancellationToken);
    }

    public async Task<PoiDetailResponse> CreateAsync(CreatePoiRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAutoTranslationCanRun(request);
        var poi = new Poi();
        ApplyRequest(poi, request);
        await _poiRepository.CreateAsync(poi, cancellationToken);

        try
        {
            await SyncAutoTranslationsAsync(poi, request, cancellationToken);
            return await MapDetailAsync(poi, SharedConstants.DefaultUiLanguage, SharedConstants.DefaultAudioLanguage, cancellationToken);
        }
        catch
        {
            await _localizationRepository.DeleteByPoiIdAsync(poi.Id, cancellationToken);
            await _poiRepository.DeleteHardAsync(poi.Id, cancellationToken);
            throw;
        }
    }

    public async Task<PoiDetailResponse> UpdateAsync(string id, UpdatePoiRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAutoTranslationCanRun(request);
        var poi = await _poiRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Khong tim thay POI.", StatusCodes.Status404NotFound);
        ApplyRequest(poi, request);
        poi.ActivationRequested = request.ActivationRequested;
        await _poiRepository.UpdateAsync(poi, cancellationToken);
        await SyncAutoTranslationsAsync(poi, request, cancellationToken);
        return await MapDetailAsync(poi, SharedConstants.DefaultUiLanguage, SharedConstants.DefaultAudioLanguage, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var poi = await _poiRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Khong tim thay POI.", StatusCodes.Status404NotFound);
        await _poiRepository.SoftDeleteAsync(poi.Id, cancellationToken);
    }

    public async Task SetActiveAsync(string id, bool isActive, CancellationToken cancellationToken = default)
    {
        var poi = await _poiRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Khong tim thay POI.", StatusCodes.Status404NotFound);
        await _poiRepository.SetActiveAsync(poi.Id, isActive, cancellationToken);
    }

    internal async Task<PoiResponse> MapAsync(Poi poi, string? lang, string? audioLang, CancellationToken cancellationToken)
    {
        var normalizedUiLang = NormalizeUiLanguage(lang);
        var normalizedAudioLang = NormalizeAudioLanguage(audioLang);
        var displayLocalization = await LoadLocalizationAsync(poi.Id, normalizedUiLang, cancellationToken);
        var requestedNarrationLocalization = string.Equals(normalizedAudioLang, normalizedUiLang, StringComparison.Ordinal)
            ? displayLocalization
            : await LoadLocalizationAsync(poi.Id, normalizedAudioLang, cancellationToken);

        return new PoiResponse
        {
            Id = poi.Id,
            Name = displayLocalization?.Name ?? poi.Name,
            Description = displayLocalization?.Description ?? poi.Description,
            CategoryId = poi.CategoryId,
            Address = poi.Address,
            Ward = poi.Ward,
            District = poi.District,
            City = poi.City,
            PriceRange = poi.PriceRange,
            Rating = poi.Rating,
            ReviewCount = poi.ReviewCount,
            Priority = poi.Priority,
            MapUrl = poi.MapUrl,
            TtsScript = await ResolveNarrationScriptAsync(
                poi,
                normalizedAudioLang,
                requestedNarrationLocalization,
                cancellationToken),
            Latitude = poi.Location.Coordinates.Latitude,
            Longitude = poi.Location.Coordinates.Longitude,
            GeofenceRadiusMeters = poi.GeofenceRadiusMeters,
            AutoNarrationEnabled = poi.AutoNarrationEnabled,
            Tags = poi.Tags,
            Images = poi.Images,
            IsActive = poi.IsActive
        };
    }

    private async Task<PoiDetailResponse> MapDetailAsync(
        Poi poi,
        string? lang,
        string? audioLang,
        CancellationToken cancellationToken)
    {
        var baseResponse = await MapAsync(poi, lang, audioLang, cancellationToken);
        return new PoiDetailResponse
        {
            Id = baseResponse.Id,
            Name = baseResponse.Name,
            Description = baseResponse.Description,
            CategoryId = baseResponse.CategoryId,
            Address = baseResponse.Address,
            Ward = baseResponse.Ward,
            District = baseResponse.District,
            City = baseResponse.City,
            PriceRange = baseResponse.PriceRange,
            Rating = baseResponse.Rating,
            ReviewCount = baseResponse.ReviewCount,
            Priority = baseResponse.Priority,
            MapUrl = baseResponse.MapUrl,
            TtsScript = baseResponse.TtsScript,
            Latitude = baseResponse.Latitude,
            Longitude = baseResponse.Longitude,
            GeofenceRadiusMeters = baseResponse.GeofenceRadiusMeters,
            AutoNarrationEnabled = baseResponse.AutoNarrationEnabled,
            Tags = baseResponse.Tags,
            Images = baseResponse.Images,
            IsActive = baseResponse.IsActive,
            OpeningHours = poi.OpeningHours,
            ContactInfo = poi.ContactInfo,
            OwnerId = poi.OwnerId,
            AudioStatus = poi.AudioStatus
        };
    }

    private async Task<List<PoiResponse>> MapManyAsync(
        IEnumerable<Poi> pois,
        string? lang,
        string? audioLang,
        CancellationToken cancellationToken)
    {
        var results = new List<PoiResponse>();
        foreach (var poi in pois)
        {
            results.Add(await MapAsync(poi, lang, audioLang, cancellationToken));
        }

        return results;
    }

    private static void ApplyRequest(Poi poi, CreatePoiRequest request)
    {
        poi.Name = request.Name;
        poi.Description = request.Description;
        poi.CategoryId = request.CategoryId;
        poi.Location = GeoLocationFactory.Create(request.Location.Longitude, request.Location.Latitude);
        poi.Address = request.Address;
        poi.Ward = request.Ward;
        poi.District = request.District;
        poi.City = request.City;
        poi.PriceRange = request.PriceRange;
        poi.Priority = request.Priority;
        poi.MapUrl = string.IsNullOrWhiteSpace(request.MapUrl) ? null : request.MapUrl.Trim();
        poi.TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim();
        poi.GeofenceRadiusMeters = request.GeofenceRadiusMeters;
        poi.AutoNarrationEnabled = request.AutoNarrationEnabled;
        poi.Images = request.Images;
        poi.OpeningHours = request.OpeningHours;
        poi.ContactInfo = request.ContactInfo;
        poi.OwnerId = request.OwnerId;
        poi.Tags = request.Tags;
        poi.IsActive = request.IsActive;
        poi.UpdatedAt = DateTime.UtcNow;
    }

    private async Task SyncAutoTranslationsAsync(Poi poi, CreatePoiRequest request, CancellationToken cancellationToken)
    {
        if (!request.AutoTranslateAudioContent)
        {
            return;
        }

        var targetLanguages = ResolveAutoTranslateLanguages(request);

        foreach (var targetLanguage in targetLanguages)
        {
            try
            {
                await _localizationService.TranslateAsync(
                    poi.Id,
                    new TranslatePoiLocalizationRequest
                    {
                        Lang = targetLanguage,
                        SourceLang = SharedConstants.DefaultAudioLanguage,
                        OverwriteExisting = request.OverwriteAutoTranslations
                    },
                    cancellationToken);
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to auto-translate POI {PoiId} into {Lang}",
                    poi.Id,
                    targetLanguage);
                throw new ApiException(
                    $"Khong the tu dong dich POI sang ngon ngu {targetLanguage}. Hay kiem tra Python translation runtime va thu lai.",
                    StatusCodes.Status502BadGateway);
            }
        }
    }

    private void EnsureAutoTranslationCanRun(CreatePoiRequest request)
    {
        if (!request.AutoTranslateAudioContent)
        {
            return;
        }

        if (!_localizationService.CanAutoTranslate())
        {
            throw new ApiException(
                "Auto-translate dang bat nhung backend chua san sang translator. Can Python translation runtime.",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (ResolveAutoTranslateLanguages(request).Count == 0)
        {
            throw new ApiException("Chua co ngon ngu dich nao duoc chon cho audio.");
        }
    }

    private static List<string> ResolveAutoTranslateLanguages(CreatePoiRequest request)
    {
        var targetLanguages = request.AutoTranslateLanguages
            .Select(NormalizeAudioLanguage)
            .Where(static lang => !string.Equals(lang, SharedConstants.DefaultAudioLanguage, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (targetLanguages.Count > 0)
        {
            return targetLanguages;
        }

        return SharedConstants.SupportedLanguages
            .Where(static lang => !string.Equals(lang, SharedConstants.DefaultAudioLanguage, StringComparison.Ordinal))
            .ToList();
    }

    private async Task<PoiLocalization?> LoadLocalizationAsync(string poiId, string lang, CancellationToken cancellationToken)
    {
        if (string.Equals(lang, SharedConstants.DefaultAudioLanguage, StringComparison.Ordinal))
        {
            return null;
        }

        var existing = await _localizationRepository.GetByPoiAndLangAsync(poiId, lang, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await _localizationService.EnsureLocalizationAsync(poiId, lang, cancellationToken);
    }

    private async Task<string?> ResolveNarrationScriptAsync(
        Poi poi,
        string requestedAudioLang,
        PoiLocalization? requestedLocalization,
        CancellationToken cancellationToken)
    {
        if (string.Equals(requestedAudioLang, SharedConstants.DefaultAudioLanguage, StringComparison.Ordinal))
        {
            return FirstNonEmpty(poi.TtsScript, poi.Description);
        }

        return FirstNonEmpty(requestedLocalization?.TtsScript, requestedLocalization?.Description);
    }

    private static string NormalizeUiLanguage(string? lang)
    {
        var normalized = string.IsNullOrWhiteSpace(lang) ? SharedConstants.DefaultUiLanguage : lang.Trim().ToLowerInvariant();
        return SharedConstants.SupportedUiLanguages.Contains(normalized) ? normalized : SharedConstants.DefaultUiLanguage;
    }

    private static string NormalizeAudioLanguage(string? lang)
    {
        var normalized = string.IsNullOrWhiteSpace(lang) ? SharedConstants.DefaultAudioLanguage : lang.Trim().ToLowerInvariant();
        return SharedConstants.SupportedLanguages.Contains(normalized) ? normalized : SharedConstants.DefaultAudioLanguage;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
