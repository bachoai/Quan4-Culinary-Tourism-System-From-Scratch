namespace Quan4CulinaryTourism.Api.Models;

public class Tour
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Lang { get; set; } = "vi";
    public string? CoverImageUrl { get; set; }
    public string? CreatedByUserId { get; set; }
    public int EstimatedDurationMinutes { get; set; } = 60;
    public bool IsActive { get; set; } = true;
    public List<TourStop> Stops { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
