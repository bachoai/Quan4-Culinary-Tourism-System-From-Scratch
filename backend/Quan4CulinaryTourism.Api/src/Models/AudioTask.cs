namespace Quan4CulinaryTourism.Api.Models;

public class AudioTask
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string TaskId { get; set; } = Guid.NewGuid().ToString("N");
    public string PoiId { get; set; } = string.Empty;
    public string Status { get; set; } = SharedConstants.AudioTaskStatuses.Queued;
    public List<string> Languages { get; set; } = [];
    public int ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public bool PauseRequested { get; set; }
    public bool CancelRequested { get; set; }
    public DateTime? HeartbeatAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

