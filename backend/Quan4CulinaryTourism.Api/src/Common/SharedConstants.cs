namespace Quan4CulinaryTourism.Api.Common;

public static class SharedConstants
{
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Owner = "Owner";
        public const string User = "User";
    }

    public static class OwnerStatuses
    {
        public const string None = "none";
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Rejected = "rejected";
    }

    public static class SubmissionStatuses
    {
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Rejected = "rejected";
    }

    public static class AudioStatuses
    {
        public const string Pending = "pending";
        public const string Processing = "processing";
        public const string Done = "done";
        public const string Failed = "failed";
    }

    public static class AudioTaskStatuses
    {
        public const string Queued = "queued";
        public const string Running = "running";
        public const string Paused = "paused";
        public const string Done = "done";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
    }

    public static class PriceRanges
    {
        public static readonly string[] Values = ["$", "$$", "$$$"];
    }

    public static class Languages
    {
        public const string DefaultUi = "vi";
        public const string DefaultAudio = "vi";

        public static readonly string[] SupportedUi = ["vi", "en"];
        public static readonly string[] Supported = ["vi", "en", "zh", "ja", "ko", "fr", "de", "es", "th", "ru"];
        public static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
        {
            ["vi"] = "Vietnamese",
            ["en"] = "English",
            ["zh"] = "Chinese",
            ["ja"] = "Japanese",
            ["ko"] = "Korean",
            ["fr"] = "French",
            ["de"] = "German",
            ["es"] = "Spanish",
            ["th"] = "Thai",
            ["ru"] = "Russian",
        };
    }

    public static class SubmissionTypes
    {
        public const string Create = "create";
        public const string Update = "update";

        public static readonly string[] Values = [Create, Update];
    }

    public static class MediaTypes
    {
        public static readonly string[] Values = ["image", "audio", "map"];
    }

    public static class StorageProviders
    {
        public const string Local = "local";
        public const string Cloudinary = "cloudinary";
        public const string Minio = "minio";
        public const string S3 = "s3";
        public const string External = "external";

        public static readonly string[] Values = [Local, Cloudinary, Minio, S3];
    }

    public static class AudioSourceTypes
    {
        public const string Uploaded = "uploaded";
        public const string PythonTts = "python_tts";
    }

    public static class QrScanModes
    {
        public const string PreferAudio = "prefer_audio";
        public const string Audio = "audio";
        public const string Tts = "tts";
        public const string ValidationPattern = "^(prefer_audio|audio|tts)$";

        public static readonly string[] Values = [PreferAudio, Audio, Tts];
    }

    public static class AnalyticsEvents
    {
        public const string PoiViewed = "poi_viewed";
        public const string AudioPlayed = "audio_played";
        public const string TtsPlayed = "tts_played";
        public const string SearchExecuted = "search_executed";
        public const string NearbyRequested = "nearby_requested";
        public const string LanguageChanged = "language_changed";
        public const string OfflineAudioDownloaded = "offline_audio_downloaded";
        public const string GeofenceTriggered = "geofence_triggered";
        public const string LocationSample = "location_sample";
        public const string NarrationCompleted = "narration_completed";
        public const string NarrationInterrupted = "narration_interrupted";
        public const string NarrationStopped = "narration_stopped";
        public const string QrScanned = "qr_scanned";
        public const string PresencePing = "presence_ping";

        public static readonly string[] Values =
        [
            PoiViewed,
            AudioPlayed,
            TtsPlayed,
            SearchExecuted,
            NearbyRequested,
            LanguageChanged,
            OfflineAudioDownloaded,
            GeofenceTriggered,
            LocationSample,
            NarrationCompleted,
            NarrationInterrupted,
            NarrationStopped
        ];
    }

    public static class AnalyticsContentTypes
    {
        public const string Audio = "audio";
        public const string Poi = "poi";

        public static readonly string[] Values = [Audio, Poi];
    }
}
