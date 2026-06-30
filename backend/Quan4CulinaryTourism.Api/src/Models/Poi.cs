using MongoDB.Driver.GeoJsonObjectModel;

namespace Quan4CulinaryTourism.Api.Models;

public class Poi
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = GeoLocationFactory.Create(0, 0);
    public string Address { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = "Quận 4";
    public string City { get; set; } = "TP. Hồ Chí Minh";
    public string PriceRange { get; set; } = "$";
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public int Priority { get; set; }
    public string? MapUrl { get; set; }
    public string? TtsScript { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 100;
    public bool AutoNarrationEnabled { get; set; } = true;
    public List<PoiImage> Images { get; set; } = [];
    public List<OpeningHour> OpeningHours { get; set; } = [];
    public ContactInfo? ContactInfo { get; set; }
    public string? OwnerId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool ActivationRequested { get; set; }
    public string AudioStatus { get; set; } = SharedConstants.AudioStatuses.Pending;
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

