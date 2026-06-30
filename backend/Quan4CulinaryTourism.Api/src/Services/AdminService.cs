using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class AdminService
{
    private readonly AuthService _authService;
    private readonly UserRepository _userRepository;
    private readonly OwnerRegistrationRepository _ownerRegistrationRepository;
    private readonly OwnerSubmissionRepository _ownerSubmissionRepository;
    private readonly PoiRepository _poiRepository;
    private readonly AnalyticsRepository _analyticsRepository;

    public AdminService(AuthService authService, UserRepository userRepository, OwnerRegistrationRepository ownerRegistrationRepository, OwnerSubmissionRepository ownerSubmissionRepository, PoiRepository poiRepository, AnalyticsRepository analyticsRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
        _ownerRegistrationRepository = ownerRegistrationRepository;
        _ownerSubmissionRepository = ownerSubmissionRepository;
        _poiRepository = poiRepository;
        _analyticsRepository = analyticsRepository;
    }

    public async Task<AuthResponse> LoginAdminAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        if (!response.User.Roles.Contains(SharedConstants.UserRoles.Admin))
        {
            throw new ApiException("Tài khoản không có quyền admin.", StatusCodes.Status403Forbidden);
        }

        return response;
    }

    public async Task<AdminDashboardResponse> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _userRepository.CountAsync(cancellationToken: cancellationToken);
        var totalOwners = await _userRepository.CountAsync(Builders<User>.Filter.AnyEq(x => x.Roles, SharedConstants.UserRoles.Owner), cancellationToken);
        var totalPois = await _poiRepository.CountAsync(cancellationToken: cancellationToken);
        var totalActivePois = await _poiRepository.CountAsync(Builders<Poi>.Filter.Eq(x => x.IsActive, true), cancellationToken);
        var realtimeSnapshot = await _analyticsRepository.GetRealtimeSnapshotAsync(cancellationToken: cancellationToken);
        return new AdminDashboardResponse
        {
            TotalUsers = totalUsers,
            TotalOwners = totalOwners,
            TotalPois = totalPois,
            TotalActivePois = totalActivePois,
            PendingOwnerRegistrations = await _ownerRegistrationRepository.CountPendingAsync(cancellationToken),
            PendingSubmissions = await _ownerSubmissionRepository.CountPendingAsync(cancellationToken),
            TotalPoiViews = await _analyticsRepository.CountByEventNameAsync("poi_viewed", cancellationToken),
            TotalAudioPlays = await _analyticsRepository.CountByEventNamesAsync(["audio_played", "tts_played"], cancellationToken),
            ActiveVisitorsNow = realtimeSnapshot.ActiveVisitorCount,
            AnonymousVisitorsNow = realtimeSnapshot.AnonymousVisitorCount,
            ActiveWindowSeconds = realtimeSnapshot.ActiveWindowSeconds
        };
    }

    public async Task<List<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default)
        => (await _userRepository.GetAllAsync(cancellationToken)).Select(user => new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Roles = user.Roles,
            IsActive = user.IsActive,
            OwnerStatus = user.OwnerStatus,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        }).ToList();

    public async Task UpdateUserStatusAsync(string id, UpdateUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy user.", StatusCodes.Status404NotFound);
        user.IsActive = request.IsActive;
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task UpdateUserRolesAsync(string id, UpdateUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy user.", StatusCodes.Status404NotFound);
        user.Roles = request.Roles.Distinct().ToList();
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task<List<OwnerRegistrationAdminResponse>> GetOwnerRegistrationsAsync(string? status, CancellationToken cancellationToken = default)
        => (await _ownerRegistrationRepository.GetByStatusAsync(status, cancellationToken)).Select(entity => new OwnerRegistrationAdminResponse
        {
            Id = entity.Id,
            UserId = entity.UserId,
            BusinessName = entity.BusinessName,
            BusinessAddress = entity.BusinessAddress,
            PhoneNumber = entity.PhoneNumber,
            Description = entity.Description,
            Status = entity.Status,
            AdminNote = entity.AdminNote,
            ReviewedBy = entity.ReviewedBy,
            ReviewedAt = entity.ReviewedAt,
            CreatedAt = entity.CreatedAt
        }).ToList();

    public async Task ApproveOwnerAsync(string adminUserId, string id, ApproveOwnerRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var registration = await _ownerRegistrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy đăng ký owner.", StatusCodes.Status404NotFound);
        if (registration.Status != SharedConstants.OwnerPending)
        {
            throw new ApiException("Chỉ có thể duyệt yêu cầu đối tác đang chờ duyệt.");
        }
        var user = await _userRepository.GetByIdAsync(registration.UserId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy user.", StatusCodes.Status404NotFound);

        registration.Status = SharedConstants.OwnerApproved;
        registration.AdminNote = request.AdminNote;
        registration.ReviewedAt = DateTime.UtcNow;
        registration.ReviewedBy = adminUserId;
        user.OwnerStatus = SharedConstants.OwnerApproved;
        if (!user.Roles.Contains(SharedConstants.UserRoles.Owner))
        {
            user.Roles.Add(SharedConstants.UserRoles.Owner);
        }

        await _ownerRegistrationRepository.UpdateAsync(registration, cancellationToken);
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task RejectOwnerAsync(string adminUserId, string id, RejectOwnerRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var registration = await _ownerRegistrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy đăng ký owner.", StatusCodes.Status404NotFound);
        if (registration.Status != SharedConstants.OwnerPending)
        {
            throw new ApiException("Chỉ có thể từ chối yêu cầu đối tác đang chờ duyệt.");
        }
        var user = await _userRepository.GetByIdAsync(registration.UserId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy user.", StatusCodes.Status404NotFound);
        registration.Status = SharedConstants.OwnerRejected;
        registration.AdminNote = request.AdminNote;
        registration.ReviewedAt = DateTime.UtcNow;
        registration.ReviewedBy = adminUserId;
        user.OwnerStatus = SharedConstants.OwnerRejected;
        await _ownerRegistrationRepository.UpdateAsync(registration, cancellationToken);
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task DisableOwnerAsync(string adminUserId, string id, CancellationToken cancellationToken = default)
    {
        var registration = await _ownerRegistrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("KhĂ´ng tĂ¬m tháº¥y Ä‘Äƒng kĂ½ owner.", StatusCodes.Status404NotFound);
        if (registration.Status != SharedConstants.OwnerApproved)
        {
            throw new ApiException("Chá»‰ cĂ³ thá»ƒ vĂ´ hiá»‡u hĂ³a Ä‘á»‘i tĂ¡c Ä‘Ă£ Ä‘Æ°á»£c duyá»‡t.");
        }

        var user = await _userRepository.GetByIdAsync(registration.UserId, cancellationToken)
            ?? throw new ApiException("KhĂ´ng tĂ¬m tháº¥y user.", StatusCodes.Status404NotFound);

        user.OwnerStatus = SharedConstants.OwnerNone;
        user.Roles = user.Roles
            .Where(role => !string.Equals(role, SharedConstants.UserRoles.Owner, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _ownerRegistrationRepository.DeleteAsync(registration.Id, cancellationToken);
    }

    public async Task<List<OwnerSubmissionResponse>> GetSubmissionsAsync(string? status, CancellationToken cancellationToken = default)
        => (await _ownerSubmissionRepository.GetByStatusAsync(status, cancellationToken)).Select(entity => new OwnerSubmissionResponse
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
        }).ToList();

    public async Task ApproveSubmissionAsync(string adminUserId, string id, ApproveSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var submission = await _ownerSubmissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy submission.", StatusCodes.Status404NotFound);
        if (submission.Status != SharedConstants.SubmissionPending)
        {
            throw new ApiException("Chỉ có thể duyệt đề xuất đang chờ duyệt.");
        }

        if (submission.SubmissionType == "create")
        {
            var poi = new Poi
            {
                Name = submission.PoiName,
                Description = submission.Description,
                CategoryId = submission.CategoryId,
                Location = submission.Location,
                Address = submission.Address,
                Ward = submission.Ward,
                District = submission.District,
                City = submission.City,
                PriceRange = submission.PriceRange,
                Priority = submission.Priority,
                MapUrl = submission.MapUrl,
                TtsScript = submission.TtsScript,
                GeofenceRadiusMeters = submission.GeofenceRadiusMeters,
                AutoNarrationEnabled = submission.AutoNarrationEnabled,
                Images = submission.Images,
                OpeningHours = submission.OpeningHours,
                ContactInfo = submission.ContactInfo,
                OwnerId = submission.OwnerId,
                Tags = submission.Tags,
                IsActive = true,
                AudioStatus = SharedConstants.AudioPending
            };
            await _poiRepository.CreateAsync(poi, cancellationToken);
            submission.PoiId = poi.Id;
        }
        else if (submission.SubmissionType == "update")
        {
            if (string.IsNullOrWhiteSpace(submission.PoiId))
            {
                throw new ApiException("Đề xuất cập nhật không có POI mục tiêu.");
            }

            var poi = await _poiRepository.GetByIdAsync(submission.PoiId, cancellationToken)
                ?? throw new ApiException("POI cần cập nhật không tồn tại.", StatusCodes.Status404NotFound);
            if (!string.Equals(poi.OwnerId, submission.OwnerId, StringComparison.Ordinal))
            {
                throw new ApiException("Không thể duyệt cập nhật cho POI không thuộc owner đã gửi đề xuất.", StatusCodes.Status403Forbidden);
            }
            poi.Name = submission.PoiName;
            poi.Description = submission.Description;
            poi.CategoryId = submission.CategoryId;
            poi.Location = submission.Location;
            poi.Address = submission.Address;
            poi.Ward = submission.Ward;
            poi.District = submission.District;
            poi.City = submission.City;
            poi.PriceRange = submission.PriceRange;
            poi.Priority = submission.Priority;
            poi.MapUrl = submission.MapUrl;
            poi.TtsScript = submission.TtsScript;
            poi.GeofenceRadiusMeters = submission.GeofenceRadiusMeters;
            poi.AutoNarrationEnabled = submission.AutoNarrationEnabled;
            poi.Images = submission.Images;
            poi.OpeningHours = submission.OpeningHours;
            poi.ContactInfo = submission.ContactInfo;
            poi.Tags = submission.Tags;
            await _poiRepository.UpdateAsync(poi, cancellationToken);
        }
        else
        {
            throw new ApiException("Loại đề xuất không hợp lệ.");
        }

        submission.Status = SharedConstants.SubmissionApproved;
        submission.AdminNote = request.AdminNote;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.ReviewedBy = adminUserId;
        await _ownerSubmissionRepository.UpdateAsync(submission, cancellationToken);
    }

    public async Task RejectSubmissionAsync(string adminUserId, string id, RejectSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var submission = await _ownerSubmissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy submission.", StatusCodes.Status404NotFound);
        if (submission.Status != SharedConstants.SubmissionPending)
        {
            throw new ApiException("Chỉ có thể từ chối đề xuất đang chờ duyệt.");
        }
        submission.Status = SharedConstants.SubmissionRejected;
        submission.AdminNote = request.AdminNote;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.ReviewedBy = adminUserId;
        await _ownerSubmissionRepository.UpdateAsync(submission, cancellationToken);
    }
}
