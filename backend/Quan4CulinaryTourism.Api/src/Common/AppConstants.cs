namespace Quan4CulinaryTourism.Api.Common;

public static class AppConstants
{
    public const string ApiVersionPrefix = "api/v1";
    public static readonly string[] SupportedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    public static readonly string[] SupportedAudioExtensions = [".mp3", ".wav", ".m4a"];
    public static readonly string[] SupportedImageMimeTypes = ["image/jpeg", "image/png", "image/webp"];
    public static readonly string[] SupportedAudioMimeTypes = ["audio/mpeg", "audio/wav", "audio/x-wav", "audio/mp4", "audio/m4a"];
    public static readonly string[] SupportedLanguages = SharedConstants.Languages.Supported;
}

