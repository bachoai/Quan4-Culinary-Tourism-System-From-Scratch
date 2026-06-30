using Quan4CulinaryTourism.Api.Common;

namespace Quan4CulinaryTourism.Api.DTOs;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public CurrentUserResponse User { get; set; } = new();
}

public class CurrentUserResponse
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool IsActive { get; set; }
    public string OwnerStatus { get; set; } = string.Empty;
}

public class UserResponse : CurrentUserResponse
{
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RoleResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
}

public class CategoryResponse
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class PoiResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PriceRange { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public int Priority { get; set; }
    public string? MapUrl { get; set; }
    public string? TtsScript { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; }
    public bool AutoNarrationEnabled { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<Quan4CulinaryTourism.Api.Models.PoiImage> Images { get; set; } = [];
    public bool IsActive { get; set; }
}

public class PoiDetailResponse : PoiResponse
{
    public List<Quan4CulinaryTourism.Api.Models.OpeningHour> OpeningHours { get; set; } = [];
    public Quan4CulinaryTourism.Api.Models.ContactInfo? ContactInfo { get; set; }
    public string? OwnerId { get; set; }
    public string AudioStatus { get; set; } = string.Empty;
}

public class NearbyPoiResponse : PoiResponse
{
    public double DistanceMeters { get; set; }
}

public class ChatSuggestResponse
{
    public string Reply { get; set; } = string.Empty;
    public List<ChatPoiSuggestionResponse> Suggestions { get; set; } = [];
}

public class ChatPoiSuggestionResponse
{
    public string PoiId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Ward { get; set; }
    public string? ImageUrl { get; set; }
    public string? Reason { get; set; }
    public double? DistanceMeters { get; set; }
    public string? DetailUrl { get; set; }
    public string? MapUrl { get; set; }
}

public class AiPoiCandidate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? Address { get; set; }
    public string? Ward { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? Description { get; set; }
    public double? DistanceMeters { get; set; }
    public string? PriceHint { get; set; }
}

internal class AiChatSuggestionPayload
{
    public string Reply { get; set; } = string.Empty;
    public List<AiChatSuggestionItem> Suggestions { get; set; } = [];
}

internal class AiChatSuggestionItem
{
    public string PoiId { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class OwnerRegistrationResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class OwnerRegistrationAdminResponse : OwnerRegistrationResponse
{
    public string? AdminNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class OwnerSubmissionResponse
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string? PoiId { get; set; }
    public string SubmissionType { get; set; } = string.Empty;
    public string PoiName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PriceRange { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? MapUrl { get; set; }
    public string? TtsScript { get; set; }
    public int GeofenceRadiusMeters { get; set; }
    public bool AutoNarrationEnabled { get; set; }
    public List<Quan4CulinaryTourism.Api.Models.PoiImage> Images { get; set; } = [];
    public List<Quan4CulinaryTourism.Api.Models.OpeningHour> OpeningHours { get; set; } = [];
    public Quan4CulinaryTourism.Api.Models.ContactInfo? ContactInfo { get; set; }
    public List<string> Tags { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OwnerDashboardResponse
{
    public int TotalPois { get; set; }
    public int TotalSubmissions { get; set; }
    public int PendingSubmissions { get; set; }
    public int ApprovedSubmissions { get; set; }
    public int RejectedSubmissions { get; set; }
    public long TotalViews { get; set; }
    public long UniqueVisitors { get; set; }
    public long TotalAudioPlays { get; set; }
    public long UniqueAudioListeners { get; set; }
    public long TotalQrScans { get; set; }
}

public class OwnerPortfolioEngagementResponse
{
    public long ViewCount { get; set; }
    public long UniqueVisitorCount { get; set; }
    public long AudioPlayCount { get; set; }
    public long UniqueAudioListenerCount { get; set; }
    public long QrScanCount { get; set; }
}

public class OwnerPoiEngagementResponse
{
    public string PoiId { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    public long UniqueVisitorCount { get; set; }
    public long AudioPlayCount { get; set; }
    public long UniqueAudioListenerCount { get; set; }
    public long QrScanCount { get; set; }
}

public class OwnerManagedPoiResponse : PoiDetailResponse
{
    public bool ActivationRequested { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long ViewCount { get; set; }
    public long UniqueVisitorCount { get; set; }
    public long AudioPlayCount { get; set; }
    public long UniqueAudioListenerCount { get; set; }
    public long QrScanCount { get; set; }
}

public class AdminDashboardResponse
{
    public long TotalUsers { get; set; }
    public long TotalOwners { get; set; }
    public long TotalPois { get; set; }
    public long TotalActivePois { get; set; }
    public long PendingOwnerRegistrations { get; set; }
    public long PendingSubmissions { get; set; }
    public long TotalPoiViews { get; set; }
    public long TotalAudioPlays { get; set; }
    public long ActiveVisitorsNow { get; set; }
    public long AnonymousVisitorsNow { get; set; }
    public int ActiveWindowSeconds { get; set; }
}

public class PoiLocalizationResponse
{
    public string Id { get; set; } = string.Empty;
    public string PoiId { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? TtsScript { get; set; }
    public bool IsFallback { get; set; }
}

public class PoiAudioResponse
{
    public string Id { get; set; } = string.Empty;
    public string PoiId { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string? VoiceName { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
}

public class AudioLanguageResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class AnalyticsSummaryResponse
{
    public long PoiViewedCount { get; set; }
    public long AudioPlayedCount { get; set; }
    public long SearchExecutedCount { get; set; }
    public double AverageListenDurationSeconds { get; set; }
    public List<TopPoiAnalyticsResponse> TopPoiViews { get; set; } = [];
    public List<TopPoiAnalyticsResponse> TopPoiAudioPlays { get; set; } = [];
    public List<AnalyticsHeatmapPointResponse> HeatmapPoints { get; set; } = [];
    public List<AnalyticsRouteTraceResponse> RecentRouteTraces { get; set; } = [];
    public AnalyticsRealtimeSnapshotResponse RealtimeSnapshot { get; set; } = new();
}

public class AnalyticsRealtimeSnapshotResponse
{
    public long ActiveVisitorCount { get; set; }
    public long AnonymousVisitorCount { get; set; }
    public long AuthenticatedVisitorCount { get; set; }
    public int ActiveWindowSeconds { get; set; }
    public List<AnalyticsActiveVisitorResponse> ActiveVisitors { get; set; } = [];
}

public class AnalyticsActiveVisitorResponse
{
    public string VisitorKey { get; set; } = string.Empty;
    public string? AnonymousId { get; set; }
    public string? SessionId { get; set; }
    public string? Lang { get; set; }
    public string? Path { get; set; }
    public string? PageTitle { get; set; }
    public bool IsAuthenticated { get; set; }
    public DateTime LastSeenAt { get; set; }
}

public class TopPoiAnalyticsResponse
{
    public string PoiId { get; set; } = string.Empty;
    public long Count { get; set; }
}

public class AnalyticsHeatmapPointResponse
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public long Count { get; set; }
    public DateTime LastSeenAt { get; set; }
}

public class AnalyticsRoutePointResponse
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsBackground { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AnalyticsRouteTraceResponse
{
    public string AnonymousId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public int PointCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public List<AnalyticsRoutePointResponse> Points { get; set; } = [];
}

public class UsageHistoryEntryResponse
{
    public string Id { get; set; } = string.Empty;
    public string? AnonymousId { get; set; }
    public string? SessionId { get; set; }
    public string? PageViewId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? PoiId { get; set; }
    public string? Lang { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class TourStopResponse
{
    public string PoiId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int Order { get; set; }
    public int EstimatedStayMinutes { get; set; }
}

public class TourResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? CreatedByUserId { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; }
    public List<TourStopResponse> Stops { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}

public class QrActivationResponse
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string PoiId { get; set; } = string.Empty;
    public string PoiName { get; set; } = string.Empty;
    public string PoiAddress { get; set; } = string.Empty;
    public string PoiWard { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StopZone { get; set; } = string.Empty;
    public string? StopAddress { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public string ScanMode { get; set; } = SharedConstants.QrScanModes.PreferAudio;
    public string DeepLink { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MediaFileResponse
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class MapPackResponse
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string EntryFile { get; set; } = "index.html";
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsActive { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class HealthResponse
{
    public string Status { get; set; } = "Healthy";
    public bool MongoConnected { get; set; }
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
}
