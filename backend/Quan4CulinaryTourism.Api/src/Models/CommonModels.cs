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

