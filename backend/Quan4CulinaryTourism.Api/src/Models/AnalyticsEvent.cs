using MongoDB.Bson.Serialization.Attributes;

namespace Quan4CulinaryTourism.Api.Models;

public class AnalyticsEvent
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string? AnonymousId { get; set; }
    public string? SessionId { get; set; }
    public string? PageViewId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? PoiId { get; set; }
    public string? Lang { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? ListenDurationSeconds { get; set; }
    public bool? IsBackground { get; set; }
    public string? TrackingSource { get; set; }
    public string? ContentType { get; set; }

    [BsonDictionaryOptions(MongoDB.Bson.Serialization.Options.DictionaryRepresentation.Document)]
    public Dictionary<string, object> Metadata { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
