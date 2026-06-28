using Microsoft.AspNetCore.Http;
using MongoDB.Driver.GeoJsonObjectModel;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class OwnerService
{
    private readonly OwnerRegistrationRepository _ownerRegistrationRepository;
    private readonly OwnerSubmissionRepository _ownerSubmissionRepository;
    private readonly PoiRepository _poiRepository;
    private readonly PoiLocalizationRepository _poiLocalizationRepository;
    private readonly AnalyticsRepository _analyticsRepository;

    public OwnerService(
        OwnerRegistrationRepository ownerRegistrationRepository,
        OwnerSubmissionRepository ownerSubmissionRepository,
        PoiRepository poiRepository,
        PoiLocalizationRepository poiLocalizationRepository,
        AnalyticsRepository analyticsRepository)
    {
        _ownerRegistrationRepository = ownerRegistrationRepository;
        _ownerSubmissionRepository = ownerSubmissionRepository;
        _poiRepository = poiRepository;
        _poiLocalizationRepository = poiLocalizationRepository;
        _analyticsRepository = analyticsRepository;
    }

    public async Task<OwnerRegistrationResponse> RegisterAsync(
        string userId,
        CreateOwnerRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = new OwnerRegistration
        {
            UserId = userId,
            BusinessName = request.BusinessName,
            BusinessAddress = request.BusinessAddress,
            PhoneNumber = request.PhoneNumber,
            Description = request.Description
        };

        await _ownerRegistrationRepository.CreateAsync(entity, cancellationToken);
        return new OwnerRegistrationResponse
        {
            Id = entity.Id,
            UserId = entity.UserId,
            BusinessName = entity.BusinessName,
            BusinessAddress = entity.BusinessAddress,
            PhoneNumber = entity.PhoneNumber,
            Description = entity.Description,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt
        };
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
            PendingSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionPending),
            ApprovedSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionApproved),
            RejectedSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionRejected),
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
        var entity = new OwnerSubmission
        {
            OwnerId = ownerId,
            PoiId = request.PoiId,
            SubmissionType = request.SubmissionType,
            PoiName = request.PoiName,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Location = GeoLocationFactory.Create(request.Location.Longitude, request.Location.Latitude),
            Address = request.Address,
            Ward = request.Ward,
            District = request.District,
            City = request.City,
            PriceRange = request.PriceRange,
            Priority = request.Priority,
            MapUrl = string.IsNullOrWhiteSpace(request.MapUrl) ? null : request.MapUrl.Trim(),
            TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim(),
            GeofenceRadiusMeters = request.GeofenceRadiusMeters,
            AutoNarrationEnabled = request.AutoNarrationEnabled,
            Images = request.Images,
            OpeningHours = request.OpeningHours,
            ContactInfo = request.ContactInfo,
            Tags = request.Tags
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
            ?? throw new ApiException("Không tìm thấy submission.", StatusCodes.Status404NotFound);

        if (entity.OwnerId != ownerId)
        {
            throw new ApiException("Không có quyền truy cập.", StatusCodes.Status403Forbidden);
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
            ?? throw new ApiException("Không tìm thấy submission.", StatusCodes.Status404NotFound);

        if (entity.OwnerId != ownerId)
        {
            throw new ApiException("Không có quyền truy cập.", StatusCodes.Status403Forbidden);
        }

        if (entity.Status != SharedConstants.SubmissionPending)
        {
            throw new ApiException("Chỉ được sửa submission đang pending.");
        }

        entity.SubmissionType = request.SubmissionType;
        entity.PoiId = request.PoiId;
        entity.PoiName = request.PoiName;
        entity.Description = request.Description;
        entity.CategoryId = request.CategoryId;
        entity.Location = GeoLocationFactory.Create(request.Location.Longitude, request.Location.Latitude);
        entity.Address = request.Address;
        entity.Ward = request.Ward;
        entity.District = request.District;
        entity.City = request.City;
        entity.PriceRange = request.PriceRange;
        entity.Priority = request.Priority;
        entity.MapUrl = string.IsNullOrWhiteSpace(request.MapUrl) ? null : request.MapUrl.Trim();
        entity.TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim();
        entity.GeofenceRadiusMeters = request.GeofenceRadiusMeters;
        entity.AutoNarrationEnabled = request.AutoNarrationEnabled;
        entity.Images = request.Images;
        entity.OpeningHours = request.OpeningHours;
        entity.ContactInfo = request.ContactInfo;
        entity.Tags = request.Tags;

        await _ownerSubmissionRepository.UpdateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

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
        Priority = entity.Priority,
        MapUrl = entity.MapUrl,
        TtsScript = entity.TtsScript,
        GeofenceRadiusMeters = entity.GeofenceRadiusMeters,
        AutoNarrationEnabled = entity.AutoNarrationEnabled,
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
}

