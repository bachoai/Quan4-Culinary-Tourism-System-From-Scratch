namespace Quan4CulinaryTourism.Api.Models;

public class PoiLocalization : BaseDocument
{
    public string PoiId { get; set; } = string.Empty;
    public string Lang { get; set; } = "vi";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? TtsScript { get; set; }
    public bool IsFallback { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
