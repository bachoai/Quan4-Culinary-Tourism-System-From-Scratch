namespace Quan4CulinaryTourism.Api.Models;

public class Tour : BaseDocument
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Lang { get; set; } = "vi";
    public string? CoverImageUrl { get; set; }
    public int EstimatedDurationMinutes { get; set; } = 60;
    public bool IsActive { get; set; } = true;
    public List<TourStop> Stops { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
