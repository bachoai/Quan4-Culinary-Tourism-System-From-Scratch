using Microsoft.AspNetCore.Http;
using MongoDB.Driver.GeoJsonObjectModel;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class OwnerService
{
    private const int MinSubmissionGeofenceRadiusMeters = 20;
    private const int MaxSubmissionGeofenceRadiusMeters = 1000;

    private readonly OwnerSubmissionRepository _ownerSubmissionRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly PoiRepository _poiRepository;
    private readonly PoiLocalizationRepository _poiLocalizationRepository;
    private readonly AnalyticsRepository _analyticsRepository;
    private readonly LocalizationService _localizationService;
    private readonly AudioService _audioService;
    private readonly MediaService _mediaService;

    public OwnerService(
        OwnerSubmissionRepository ownerSubmissionRepository,
        CategoryRepository categoryRepository,
        PoiRepository poiRepository,
        PoiLocalizationRepository poiLocalizationRepository,
        AnalyticsRepository analyticsRepository,
        LocalizationService localizationService,
        AudioService audioService,
        MediaService mediaService)
    {
        _ownerSubmissionRepository = ownerSubmissionRepository;
        _categoryRepository = categoryRepository;
        _poiRepository = poiRepository;
        _poiLocalizationRepository = poiLocalizationRepository;
        _analyticsRepository = analyticsRepository;
        _localizationService = localizationService;
        _audioService = audioService;
        _mediaService = mediaService;
    }

    public async Task<OwnerDashboardResponse> GetDashboardAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var ownerPois = await _poiRepository.GetByOwnerIdAsync(ownerId, cancellationToken);
        var submissions = await _ownerSubmissionRepository.GetByOwnerIdAsync(ownerId, cancellationToken);
        var poiIds = ownerPois.Select(static poi => poi.Id).ToList();
        var engagement = poiIds.Count == 0
            ? new OwnerPortfolioEngagementResponse()
            : await _analyticsRepository.GetPortfolioEngagementStatsAsync(poiIds, cancellationToken);

        return new OwnerDashboardResponse
        {
            TotalPois = ownerPois.Count,
            TotalSubmissions = submissions.Count,
            PendingSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionStatuses.Pending),
            ApprovedSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionStatuses.Approved),
            RejectedSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionStatuses.Rejected),
            TotalViews = engagement.ViewCount,
            UniqueVisitors = engagement.UniqueVisitorCount,
            TotalAudioPlays = engagement.AudioPlayCount,
            UniqueAudioListeners = engagement.UniqueAudioListenerCount,
            TotalQrScans = engagement.QrScanCount
        };
    }

    public async Task<List<OwnerManagedPoiResponse>> GetMyPoisAsync(
        string ownerId,
        string? lang = null,
        CancellationToken cancellationToken = default)
    {
        var pois = await _poiRepository.GetByOwnerIdAsync(ownerId, cancellationToken);
        if (pois.Count == 0)
        {
            return [];
        }

        var statsLookup = await _analyticsRepository.GetPoiEngagementStatsAsync(
            pois.Select(static poi => poi.Id),
            cancellationToken);

        var responses = new List<OwnerManagedPoiResponse>(pois.Count);
        foreach (var poi in pois.OrderByDescending(static poi => poi.UpdatedAt).ThenByDescending(static poi => poi.Priority))
        {
            statsLookup.TryGetValue(poi.Id, out var stats);
            responses.Add(await MapManagedPoiAsync(poi, stats, lang, cancellationToken));
        }

        return responses;
    }

    public async Task<OwnerSubmissionResponse> CreateSubmissionAsync(
        string ownerId,
        CreateOwnerSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = await NormalizeSubmissionRequestAsync(ownerId, request, cancellationToken);
        var entity = new OwnerSubmission
        {
            OwnerId = ownerId,
            PoiId = normalizedRequest.PoiId,
            SubmissionType = normalizedRequest.SubmissionType,
            PoiName = normalizedRequest.PoiName,
            Description = normalizedRequest.Description,
            CategoryId = normalizedRequest.CategoryId,
            Location = GeoLocationFactory.Create(normalizedRequest.Location.Longitude, normalizedRequest.Location.Latitude),
            Address = normalizedRequest.Address,
            Ward = normalizedRequest.Ward,
            District = normalizedRequest.District,
            City = normalizedRequest.City,
            PriceRange = normalizedRequest.PriceRange,
            Priority = normalizedRequest.Priority,
            MapUrl = normalizedRequest.MapUrl,
            TtsScript = normalizedRequest.TtsScript,
            GeofenceRadiusMeters = normalizedRequest.GeofenceRadiusMeters,
            AutoNarrationEnabled = normalizedRequest.AutoNarrationEnabled,
            Images = normalizedRequest.Images,
            OpeningHours = normalizedRequest.OpeningHours,
            ContactInfo = normalizedRequest.ContactInfo,
            Tags = normalizedRequest.Tags
        };

        await _ownerSubmissionRepository.CreateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public async Task<List<OwnerSubmissionResponse>> GetMySubmissionsAsync(
        string ownerId,
        CancellationToken cancellationToken = default) =>
        (await _ownerSubmissionRepository.GetByOwnerIdAsync(ownerId, cancellationToken))
        .Select(ToResponse)
        .ToList();

    public async Task<OwnerSubmissionResponse> GetMySubmissionByIdAsync(
        string ownerId,
        string id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _ownerSubmissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không t?m th?y submission.", StatusCodes.Status404NotFound);

        if (entity.OwnerId != ownerId)
        {
            throw new ApiException("Không có quy?n truy c?p.", StatusCodes.Status403Forbidden);
        }

        return ToResponse(entity);
    }

    public async Task<OwnerSubmissionResponse> UpdateMySubmissionAsync(
        string ownerId,
        string id,
        CreateOwnerSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _ownerSubmissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không t?m th?y submission.", StatusCodes.Status404NotFound);

        if (entity.OwnerId != ownerId)
        {
            throw new ApiException("Không có quy?n truy c?p.", StatusCodes.Status403Forbidden);
        }

        if (entity.Status != SharedConstants.SubmissionStatuses.Pending)
        {
            throw new ApiException("Ch? đư?c s?a đ? xu?t đang ch? duy?t.");
        }

        var normalizedRequest = await NormalizeSubmissionRequestAsync(ownerId, request, cancellationToken);
        entity.SubmissionType = normalizedRequest.SubmissionType;
        entity.PoiId = normalizedRequest.PoiId;
        entity.PoiName = normalizedRequest.PoiName;
        entity.Description = normalizedRequest.Description;
        entity.CategoryId = normalizedRequest.CategoryId;
        entity.Location = GeoLocationFactory.Create(normalizedRequest.Location.Longitude, normalizedRequest.Location.Latitude);
        entity.Address = normalizedRequest.Address;
        entity.Ward = normalizedRequest.Ward;
        entity.District = normalizedRequest.District;
        entity.City = normalizedRequest.City;
        entity.PriceRange = normalizedRequest.PriceRange;
        entity.Priority = normalizedRequest.Priority;
        entity.MapUrl = normalizedRequest.MapUrl;
        entity.TtsScript = normalizedRequest.TtsScript;
        entity.GeofenceRadiusMeters = normalizedRequest.GeofenceRadiusMeters;
        entity.AutoNarrationEnabled = normalizedRequest.AutoNarrationEnabled;
        entity.Images = normalizedRequest.Images;
        entity.OpeningHours = normalizedRequest.OpeningHours;
        entity.ContactInfo = normalizedRequest.ContactInfo;
        entity.Tags = normalizedRequest.Tags;

        await _ownerSubmissionRepository.UpdateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public async Task<List<PoiLocalizationResponse>> GetMyPoiLocalizationsAsync(
        string ownerId,
        string poiId,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireOwnedPoiAsync(ownerId, poiId, cancellationToken);
        return await _localizationService.GetPoiLocalizationsAsync(poiId, cancellationToken);
    }

    public async Task<PoiLocalizationResponse> UpsertMyPoiLocalizationAsync(
        string ownerId,
        string poiId,
        CreatePoiLocalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireOwnedPoiAsync(ownerId, poiId, cancellationToken);
        if (string.Equals(request.Lang, "vi", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException("Owner khong duoc sua localization tieng Viet truc tiep.");
        }

        return await _localizationService.UpsertAsync(poiId, request, cancellationToken);
    }

    public async Task<PoiLocalizationResponse> TranslateMyPoiLocalizationAsync(
        string ownerId,
        string poiId,
        TranslatePoiLocalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireOwnedPoiAsync(ownerId, poiId, cancellationToken);
        if (string.Equals(request.Lang, "vi", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException("Flow translate cua owner chi danh cho ngon ngu khac tieng Viet.");
        }

        return await _localizationService.TranslateAsync(poiId, request, cancellationToken);
    }

    public async Task<PoiAudioResponse> UploadOrSetMyPoiAudioAsync(
        string ownerId,
        string poiId,
        UploadPoiAudioRequest request,
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireOwnedPoiAsync(ownerId, poiId, cancellationToken);
        return await _audioService.UploadOrSetAudioAsync(poiId, request, file, cancellationToken);
    }

    public async Task<PoiAudioResponse> GenerateMyPoiAudioAsync(
        string ownerId,
        string poiId,
        GeneratePoiAudioRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireOwnedPoiAsync(ownerId, poiId, cancellationToken);
        return await _audioService.GeneratePoiAudioAsync(poiId, request, cancellationToken);
    }

    public async Task DeleteMyPoiAudioAsync(
        string ownerId,
        string poiId,
        string? lang,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireOwnedPoiAsync(ownerId, poiId, cancellationToken);
        await _audioService.DeletePoiAudioAsync(poiId, lang, cancellationToken);
    }

    public Task<MediaFileResponse> UploadMyImageAsync(
        string ownerId,
        IFormFile file,
        CancellationToken cancellationToken = default) =>
        _mediaService.UploadImageAsync(file, ownerId, cancellationToken);

    private async Task<OwnerManagedPoiResponse> MapManagedPoiAsync(
        Poi poi,
        OwnerPoiEngagementResponse? stats,
        string? lang,
        CancellationToken cancellationToken)
    {
        var localization = !string.IsNullOrWhiteSpace(lang)
            ? await _poiLocalizationRepository.GetByPoiAndLangAsync(poi.Id, lang, cancellationToken)
            : null;

        return new OwnerManagedPoiResponse
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
            TtsScript = ResolveNarrationScript(lang, poi.TtsScript, localization?.TtsScript),
            Latitude = poi.Location.Coordinates.Latitude,
            Longitude = poi.Location.Coordinates.Longitude,
            GeofenceRadiusMeters = poi.GeofenceRadiusMeters,
            AutoNarrationEnabled = poi.AutoNarrationEnabled,
            Tags = poi.Tags,
            Images = poi.Images,
            IsActive = poi.IsActive,
            OpeningHours = poi.OpeningHours,
            ContactInfo = poi.ContactInfo,
            OwnerId = poi.OwnerId,
            AudioStatus = poi.AudioStatus,
            ActivationRequested = poi.ActivationRequested,
            CreatedAt = poi.CreatedAt,
            UpdatedAt = poi.UpdatedAt,
            ViewCount = stats?.ViewCount ?? 0,
            UniqueVisitorCount = stats?.UniqueVisitorCount ?? 0,
            AudioPlayCount = stats?.AudioPlayCount ?? 0,
            UniqueAudioListenerCount = stats?.UniqueAudioListenerCount ?? 0,
            QrScanCount = stats?.QrScanCount ?? 0
        };
    }

    private static OwnerSubmissionResponse ToResponse(OwnerSubmission entity) => new()
    {
        Id = entity.Id,
        OwnerId = entity.OwnerId,
        PoiId = entity.PoiId,
        SubmissionType = entity.SubmissionType,
        PoiName = entity.PoiName,
        Description = entity.Description,
        CategoryId = entity.CategoryId,
        Latitude = entity.Location.Coordinates.Latitude,
        Longitude = entity.Location.Coordinates.Longitude,
        Address = entity.Address,
        Ward = entity.Ward,
        District = entity.District,
        City = entity.City,
        PriceRange = entity.PriceRange,
        Priority = entity.Priority,
        MapUrl = entity.MapUrl,
        TtsScript = entity.TtsScript,
        GeofenceRadiusMeters = entity.GeofenceRadiusMeters,
        AutoNarrationEnabled = entity.AutoNarrationEnabled,
        Images = entity.Images,
        OpeningHours = entity.OpeningHours,
        ContactInfo = entity.ContactInfo,
        Tags = entity.Tags,
        Status = entity.Status,
        AdminNote = entity.AdminNote,
        CreatedAt = entity.CreatedAt
    };

    private static string? ResolveNarrationScript(string? lang, string? baseScript, string? localizedScript)
    {
        if (string.Equals(lang, "vi", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(lang))
        {
            return string.IsNullOrWhiteSpace(localizedScript) ? baseScript : localizedScript;
        }

        return string.IsNullOrWhiteSpace(localizedScript) ? null : localizedScript;
    }

    private async Task<Poi> RequireOwnedPoiAsync(
        string ownerId,
        string poiId,
        CancellationToken cancellationToken)
    {
        var poi = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Khong tim thay POI.", StatusCodes.Status404NotFound);
        if (!string.Equals(poi.OwnerId, ownerId, StringComparison.Ordinal))
        {
            throw new ApiException("Ban khong duoc quan ly POI nay.", StatusCodes.Status403Forbidden);
        }

        return poi;
    }

    private async Task<CreateOwnerSubmissionRequest> NormalizeSubmissionRequestAsync(
        string ownerId,
        CreateOwnerSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var submissionType = request.SubmissionType.Trim().ToLowerInvariant();
        if (!SharedConstants.SubmissionTypes.Values.Contains(submissionType))
        {
            throw new ApiException("Lo?i đ? xu?t không h?p l?. Ch? h? tr? create ho?c update.");
        }

        if (request.GeofenceRadiusMeters < MinSubmissionGeofenceRadiusMeters || request.GeofenceRadiusMeters > MaxSubmissionGeofenceRadiusMeters)
        {
            throw new ApiException($"Bán kính geofence ph?i trong kho?ng {MinSubmissionGeofenceRadiusMeters}-{MaxSubmissionGeofenceRadiusMeters} mét.");
        }

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId.Trim(), cancellationToken);
        if (category is null)
        {
            throw new ApiException("Danh m?c không t?n t?i.", StatusCodes.Status404NotFound);
        }

        string? poiId = string.IsNullOrWhiteSpace(request.PoiId) ? null : request.PoiId.Trim();
        if (submissionType == SharedConstants.SubmissionTypes.Update)
        {
            if (poiId is null)
            {
                throw new ApiException("Đ? xu?t c?p nh?t b?t bu?c ph?i có POI.");
            }

            var poi = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
                ?? throw new ApiException("POI c?n c?p nh?t không t?n t?i.", StatusCodes.Status404NotFound);
            if (!string.Equals(poi.OwnerId, ownerId, StringComparison.Ordinal))
            {
                throw new ApiException("B?n không th? c?p nh?t POI không thu?c quy?n qu?n l? c?a m?nh.", StatusCodes.Status403Forbidden);
            }
        }
        else
        {
            poiId = null;
        }

        return new CreateOwnerSubmissionRequest
        {
            SubmissionType = submissionType,
            PoiId = poiId,
            PoiName = request.PoiName.Trim(),
            Description = request.Description.Trim(),
            CategoryId = category.Id,
            Location = request.Location,
            Address = request.Address.Trim(),
            Ward = request.Ward.Trim(),
            District = request.District.Trim(),
            City = request.City.Trim(),
            PriceRange = request.PriceRange,
            Priority = request.Priority,
            MapUrl = TrimOrNull(request.MapUrl),
            TtsScript = TrimOrNull(request.TtsScript),
            GeofenceRadiusMeters = request.GeofenceRadiusMeters,
            AutoNarrationEnabled = request.AutoNarrationEnabled,
            Images = request.Images,
            OpeningHours = request.OpeningHours,
            ContactInfo = request.ContactInfo,
            Tags = request.Tags
                .Select(static tag => tag.Trim())
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
