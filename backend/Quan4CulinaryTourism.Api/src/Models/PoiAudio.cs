namespace Quan4CulinaryTourism.Api.Models;

public class PoiAudio : BaseDocument
{
    public string PoiId { get; set; } = string.Empty;
    public string Lang { get; set; } = "vi";
    public bool IsDeleted { get; set; }
    public string AudioUrl { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = "local";
    public string? ObjectKey { get; set; }
    public string? ResourceType { get; set; }
    public string? VoiceName { get; set; }
    public string SourceType { get; set; } = "uploaded";
    public string? NarrationSignature { get; set; }
    public string Status { get; set; } = SharedConstants.AudioPending;
    public double DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
