using MongoDB.Driver.GeoJsonObjectModel;

namespace Quan4CulinaryTourism.Api.Models;

public class OwnerSubmission
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;
    public string? PoiId { get; set; }
    public string SubmissionType { get; set; } = SharedConstants.SubmissionTypes.Create;
    public string PoiName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = GeoLocationFactory.Create(0, 0);
    public string Address { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = "Quận 4";
    public string City { get; set; } = "TP. Hồ Chí Minh";
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
    public string Status { get; set; } = SharedConstants.SubmissionStatuses.Pending;
    public string? AdminNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

