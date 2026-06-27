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
    private readonly AnalyticsRepository _analyticsRepository;

    public OwnerService(OwnerRegistrationRepository ownerRegistrationRepository, OwnerSubmissionRepository ownerSubmissionRepository, PoiRepository poiRepository, AnalyticsRepository analyticsRepository)
    {
        _ownerRegistrationRepository = ownerRegistrationRepository;
        _ownerSubmissionRepository = ownerSubmissionRepository;
        _poiRepository = poiRepository;
        _analyticsRepository = analyticsRepository;
    }

    public async Task<OwnerRegistrationResponse> RegisterAsync(string userId, CreateOwnerRegistrationRequest request, CancellationToken cancellationToken = default)
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

    public async Task<OwnerDashboardResponse> GetDashboardAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var ownerPois = await _poiRepository.GetByOwnerIdAsync(ownerId, cancellationToken);
        var submissions = await _ownerSubmissionRepository.GetByOwnerIdAsync(ownerId, cancellationToken);
        var poiIds = ownerPois.Select(x => x.Id).ToList();

        return new OwnerDashboardResponse
        {
            TotalPois = ownerPois.Count,
            TotalSubmissions = submissions.Count,
            PendingSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionPending),
            ApprovedSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionApproved),
            RejectedSubmissions = submissions.Count(x => x.Status == SharedConstants.SubmissionRejected),
            TotalViews = poiIds.Count == 0 ? 0 : await _analyticsRepository.CountByEventNameAndPoiIdsAsync("poi_viewed", poiIds, cancellationToken),
            TotalAudioPlays = poiIds.Count == 0 ? 0 : await _analyticsRepository.CountByEventNameAndPoiIdsAsync("audio_played", poiIds, cancellationToken)
        };
    }

    public async Task<OwnerSubmissionResponse> CreateSubmissionAsync(string ownerId, CreateOwnerSubmissionRequest request, CancellationToken cancellationToken = default)
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

    public async Task<List<OwnerSubmissionResponse>> GetMySubmissionsAsync(string ownerId, CancellationToken cancellationToken = default)
        => (await _ownerSubmissionRepository.GetByOwnerIdAsync(ownerId, cancellationToken)).Select(ToResponse).ToList();

    public async Task<OwnerSubmissionResponse> GetMySubmissionByIdAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    {
        var entity = await _ownerSubmissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy submission.", StatusCodes.Status404NotFound);
        if (entity.OwnerId != ownerId)
        {
            throw new ApiException("Không có quyền truy cập.", StatusCodes.Status403Forbidden);
        }

        return ToResponse(entity);
    }

    public async Task<OwnerSubmissionResponse> UpdateMySubmissionAsync(string ownerId, string id, CreateOwnerSubmissionRequest request, CancellationToken cancellationToken = default)
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
}
