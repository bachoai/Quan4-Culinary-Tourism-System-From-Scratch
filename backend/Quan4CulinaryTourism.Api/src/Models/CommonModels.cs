using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace Quan4CulinaryTourism.Api.Models;

public class OpeningHour
{
    public string DayOfWeek { get; set; } = string.Empty;
    public string OpenTime { get; set; } = string.Empty;
    public string CloseTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}

public class PoiImage
{
    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public bool IsThumbnail { get; set; }
}

public class ContactInfo
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FacebookUrl { get; set; }
    public string? WebsiteUrl { get; set; }
}

public class TourStop
{
    public string PoiId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int Order { get; set; }
    public int EstimatedStayMinutes { get; set; } = 15;
}

public static class GeoLocationFactory
{
    public static GeoJsonPoint<GeoJson2DGeographicCoordinates> Create(double longitude, double latitude) =>
        new(new GeoJson2DGeographicCoordinates(longitude, latitude));
}

public abstract class BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
}

public static class SharedConstants
{
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Owner = "Owner";
        public const string User = "User";
    }

    public const string OwnerNone = "none";
    public const string OwnerPending = "pending";
    public const string OwnerApproved = "approved";
    public const string OwnerRejected = "rejected";

    public const string SubmissionPending = "pending";
    public const string SubmissionApproved = "approved";
    public const string SubmissionRejected = "rejected";

    public const string AudioPending = "pending";
    public const string AudioProcessing = "processing";
    public const string AudioDone = "done";
    public const string AudioFailed = "failed";

    public const string AudioTaskQueued = "queued";
    public const string AudioTaskRunning = "running";
    public const string AudioTaskPaused = "paused";
    public const string AudioTaskDone = "done";
    public const string AudioTaskFailed = "failed";
    public const string AudioTaskCancelled = "cancelled";

    public static readonly string[] PriceRanges = ["$", "$$", "$$$"];
    public static readonly string[] SupportedLanguages = ["vi", "en", "zh", "ja", "ko"];
    public static readonly string[] SubmissionTypes = ["create", "update"];
    public static readonly string[] MediaTypes = ["image", "audio", "map"];
    public static readonly string[] StorageProviders = ["local", "cloudinary", "minio", "s3"];
    public static readonly string[] QrScanModes = ["prefer_audio", "audio", "tts"];
    public static readonly string[] AnalyticsEvents =
    [
        "poi_viewed",
        "audio_played",
        "tts_played",
        "search_executed",
        "nearby_requested",
        "language_changed",
        "offline_audio_downloaded",
        "geofence_triggered",
        "location_sample",
        "narration_completed",
        "narration_interrupted",
        "narration_stopped"
    ];
}
