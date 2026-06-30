using Quan4CulinaryTourism.Api.Common;

namespace Quan4CulinaryTourism.Api.Models;

public class QrActivation
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public string PoiId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StopZone { get; set; } = string.Empty;
    public string? StopAddress { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public string ScanMode { get; set; } = SharedConstants.QrScanModes.PreferAudio;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
