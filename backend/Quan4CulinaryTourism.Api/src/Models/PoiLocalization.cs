using Quan4CulinaryTourism.Api.Common;

namespace Quan4CulinaryTourism.Api.Models;

public class PoiLocalization
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string PoiId { get; set; } = string.Empty;
    public string Lang { get; set; } = SharedConstants.Languages.DefaultUi;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? TtsScript { get; set; }
    public bool IsFallback { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
