using Quan4CulinaryTourism.Mobile.Helpers;

namespace Quan4CulinaryTourism.Mobile.Models;

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
    public List<string> Tags { get; set; } = [];
    public List<PoiImage> Images { get; set; } = [];
    public bool IsActive { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool HasAudio { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 100;
    public bool AutoNarrationEnabled { get; set; } = true;

    public string DisplayAddress =>
        string.Join(", ", new[] { Address, Ward, District, City }.Where(static value => !string.IsNullOrWhiteSpace(value)));

    public string ThumbnailUrl =>
        MediaHelper.GetPoiImageUrlOrPlaceholder(
            Images.FirstOrDefault(static image => image.IsThumbnail)?.Url
            ?? Images.FirstOrDefault()?.Url);

    public string HeroImageUrl => ThumbnailUrl;

    public string ResolvedMapUrl =>
        !string.IsNullOrWhiteSpace(MapUrl)
            ? MapUrl
            : $"https://www.google.com/maps/search/?api=1&query={Latitude},{Longitude}";

    public string NarrationText => string.IsNullOrWhiteSpace(TtsScript) ? Description : TtsScript;

    public string RatingText => Rating > 0 ? $"{Rating:0.0} ({ReviewCount})" : "Mới";
}
