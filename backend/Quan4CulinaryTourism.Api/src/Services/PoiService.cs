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
    private readonly PoiAudioRepository _audioRepository;
    private readonly DistanceHelper _distanceHelper;

    public PoiService(PoiRepository poiRepository, PoiLocalizationRepository localizationRepository, PoiAudioRepository audioRepository, DistanceHelper distanceHelper)
    {
        _poiRepository = poiRepository;
        _localizationRepository = localizationRepository;
        _audioRepository = audioRepository;
        _distanceHelper = distanceHelper;
    }

    public async Task<List<PoiResponse>> LoadAllAsync(PoiSearchRequest request, CancellationToken cancellationToken = default)
    {
        var pois = await _poiRepository.SearchAsync(request, true, cancellationToken);
        return await MapManyAsync(pois, request.Lang, cancellationToken);
    }

    public async Task<PoiDetailResponse> GetByIdAsync(string id, string? lang = null, CancellationToken cancellationToken = default)
    {
        var poi = await _poiRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);
        return await MapDetailAsync(poi, lang, cancellationToken);
    }

    public async Task<List<NearbyPoiResponse>> NearbyAsync(double lat, double lng, int radius, int limit, string? lang, CancellationToken cancellationToken = default)
    {
        if (radius is <= 0 or > 10000)
        {
            throw new ApiException("Radius phải từ 1 đến 10000 mét.");
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

            var mapped = await MapAsync(poi, lang, cancellationToken);
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
        return await MapManyAsync(pois, request.Lang, cancellationToken);
    }

    public async Task<PoiDetailResponse> CreateAsync(CreatePoiRequest request, CancellationToken cancellationToken = default)
    {
        var poi = new Poi();
        ApplyRequest(poi, request);
        await _poiRepository.CreateAsync(poi, cancellationToken);
        return await MapDetailAsync(poi, null, cancellationToken);
    }

    public async Task<PoiDetailResponse> UpdateAsync(string id, UpdatePoiRequest request, CancellationToken cancellationToken = default)
    {
        var poi = await _poiRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);
        ApplyRequest(poi, request);
        poi.ActivationRequested = request.ActivationRequested;
        await _poiRepository.UpdateAsync(poi, cancellationToken);
        return await MapDetailAsync(poi, null, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var poi = await _poiRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);
        await _poiRepository.SoftDeleteAsync(poi.Id, cancellationToken);
    }

    public async Task SetActiveAsync(string id, bool isActive, CancellationToken cancellationToken = default)
    {
        var poi = await _poiRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);
        await _poiRepository.SetActiveAsync(poi.Id, isActive, cancellationToken);
    }

    internal async Task<PoiResponse> MapAsync(Poi poi, string? lang, CancellationToken cancellationToken)
    {
        var localization = !string.IsNullOrWhiteSpace(lang)
            ? await _localizationRepository.GetByPoiAndLangAsync(poi.Id, lang, cancellationToken)
            : null;

        return new PoiResponse
        {
            Id = poi.Id,
            Name = localization?.Name ?? poi.Name,
            Description = localization?.Description ?? poi.Description,
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
            TtsScript = string.IsNullOrWhiteSpace(localization?.TtsScript) ? poi.TtsScript : localization!.TtsScript,
            Latitude = poi.Location.Coordinates.Latitude,
            Longitude = poi.Location.Coordinates.Longitude,
            GeofenceRadiusMeters = poi.GeofenceRadiusMeters,
            AutoNarrationEnabled = poi.AutoNarrationEnabled,
            Tags = poi.Tags,
            Images = poi.Images,
            IsActive = poi.IsActive
        };
    }

    private async Task<PoiDetailResponse> MapDetailAsync(Poi poi, string? lang, CancellationToken cancellationToken)
    {
        var baseResponse = await MapAsync(poi, lang, cancellationToken);
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

    private async Task<List<PoiResponse>> MapManyAsync(IEnumerable<Poi> pois, string? lang, CancellationToken cancellationToken)
    {
        var results = new List<PoiResponse>();
        foreach (var poi in pois)
        {
            results.Add(await MapAsync(poi, lang, cancellationToken));
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
}
