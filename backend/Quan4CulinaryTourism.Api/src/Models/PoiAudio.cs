namespace Quan4CulinaryTourism.Api.Models;

public class PoiAudio
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string PoiId { get; set; } = string.Empty;
    public string Lang { get; set; } = SharedConstants.Languages.DefaultUi;
    public bool IsDeleted { get; set; }
    public string AudioUrl { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = SharedConstants.StorageProviders.Local;
    public string? ObjectKey { get; set; }
    public string? ResourceType { get; set; }
    public string? VoiceName { get; set; }
    public string SourceType { get; set; } = SharedConstants.AudioSourceTypes.Uploaded;
    public string? NarrationSignature { get; set; }
    public string Status { get; set; } = SharedConstants.AudioStatuses.Pending;
    public double DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

