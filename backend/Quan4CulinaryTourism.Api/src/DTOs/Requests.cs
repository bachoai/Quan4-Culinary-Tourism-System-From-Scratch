using System.ComponentModel.DataAnnotations;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.DTOs;

public class RegisterRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class CreateCategoryRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateCategoryRequest : CreateCategoryRequest
{
    public bool IsActive { get; set; } = true;
}

public class CoordinateRequest
{
    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }
}

public class CreatePoiRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string CategoryId { get; set; } = string.Empty;

    [Required]
    public CoordinateRequest Location { get; set; } = new();

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    public string Ward { get; set; } = string.Empty;

    public string District { get; set; } = "Quận 4";
    public string City { get; set; } = "TP.HCM";

    [RegularExpression(@"^\${1,3}$")]
    public string PriceRange { get; set; } = "$";

    public int Priority { get; set; }
    public string? MapUrl { get; set; }
    public string? TtsScript { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 100;
    public bool AutoNarrationEnabled { get; set; } = true;
    public List<PoiImage> Images { get; set; } = [];
    public List<OpeningHour> OpeningHours { get; set; } = [];
    public ContactInfo? ContactInfo { get; set; }
    public string? OwnerId { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public bool AutoTranslateAudioContent { get; set; } = true;
    public bool OverwriteAutoTranslations { get; set; }
    public List<string> AutoTranslateLanguages { get; set; } = [];
}

public class UpdatePoiRequest : CreatePoiRequest
{
    public bool ActivationRequested { get; set; }
}

public class PoiSearchRequest : PaginationParams
{
    public string? Lang { get; set; }
    public string? AudioLang { get; set; }
    public string? Keyword { get; set; }
    public string? CategoryId { get; set; }
    public string? PriceRange { get; set; }
}

public class ChatSuggestRequest
{
    public string Message { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ConversationId { get; set; }
}

public class CreateOwnerRegistrationRequest
{
    [Required]
    public string BusinessName { get; set; } = string.Empty;

    [Required]
    public string BusinessAddress { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public class CreateOwnerSubmissionRequest
{
    [Required]
    public string SubmissionType { get; set; } = "create";

    public string? PoiId { get; set; }

    [Required]
    public string PoiName { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string CategoryId { get; set; } = string.Empty;

    [Required]
    public CoordinateRequest Location { get; set; } = new();

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    public string Ward { get; set; } = string.Empty;

    public string District { get; set; } = "Quận 4";
    public string City { get; set; } = "TP.HCM";

    [RegularExpression(@"^\${1,3}$")]
    public string PriceRange { get; set; } = "$";

    public int Priority { get; set; }
    public string? MapUrl { get; set; }
    public string? TtsScript { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 100;
    public bool AutoNarrationEnabled { get; set; } = true;
    public List<PoiImage> Images { get; set; } = [];
    public List<OpeningHour> OpeningHours { get; set; } = [];
    public ContactInfo? ContactInfo { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class ApproveOwnerRegistrationRequest
{
    public string? AdminNote { get; set; }
}

public class RejectOwnerRegistrationRequest
{
    [Required]
    public string AdminNote { get; set; } = string.Empty;
}

public class ApproveSubmissionRequest
{
    public string? AdminNote { get; set; }
}

public class RejectSubmissionRequest
{
    [Required]
    public string AdminNote { get; set; } = string.Empty;
}

public class CreatePoiLocalizationRequest
{
    [Required]
    public string Lang { get; set; } = "vi";

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? AudioUrl { get; set; }
    public string? TtsScript { get; set; }
    public bool IsFallback { get; set; }
}

public class UpdatePoiLocalizationRequest : CreatePoiLocalizationRequest;

public class TranslatePoiLocalizationRequest
{
    [Required]
    public string Lang { get; set; } = "en";

    public string SourceLang { get; set; } = "vi";

    public bool OverwriteExisting { get; set; } = true;
}

public class UploadPoiAudioRequest
{
    [Required]
    public string Lang { get; set; } = "vi";

    public string? AudioUrl { get; set; }
    public string? VoiceName { get; set; }
    public string SourceType { get; set; } = "uploaded";
}

public class GeneratePoiAudioRequest
{
    [Required]
    public string Lang { get; set; } = "vi";

    public string? VoiceName { get; set; }
}

public class CollectAnalyticsRequest
{
    [Required]
    public string EventName { get; set; } = string.Empty;

    public string? AnonymousId { get; set; }
    public string? SessionId { get; set; }
    public string? PageViewId { get; set; }
    public string? PoiId { get; set; }
    public string? Lang { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class CreateQrActivationRequest
{
    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string PoiId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string StopZone { get; set; } = string.Empty;

    public string? StopAddress { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    public string? Description { get; set; }

    [RegularExpression("^(prefer_audio|audio|tts)$")]
    public string ScanMode { get; set; } = "prefer_audio";

    public bool IsActive { get; set; } = true;
}

public class UpdateQrActivationRequest : CreateQrActivationRequest;

public class UsageHistoryRequest : PaginationParams
{
    public string? EventName { get; set; }
    public string? PoiId { get; set; }
    public string? Lang { get; set; }
}

public class TourStopRequest
{
    [Required]
    public string PoiId { get; set; } = string.Empty;

    public string? Title { get; set; }

    [Range(0, int.MaxValue)]
    public int Order { get; set; }

    [Range(1, 1440)]
    public int EstimatedStayMinutes { get; set; } = 15;
}

public class CreateTourRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Lang { get; set; } = "vi";

    public string? CoverImageUrl { get; set; }

    [Range(1, 1440)]
    public int EstimatedDurationMinutes { get; set; } = 60;

    public bool IsActive { get; set; } = true;

    public List<TourStopRequest> Stops { get; set; } = [];
}

public class UpdateTourRequest : CreateTourRequest;

public class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}

public class UpdateUserRolesRequest
{
    [Required]
    public List<string> Roles { get; set; } = [];
}
