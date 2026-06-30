namespace Quan4CulinaryTourism.Api.Models;

[MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElementsAttribute]
public class User
{
    [MongoDB.Bson.Serialization.Attributes.BsonIdAttribute]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentationAttribute(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> Roles { get; set; } = [SharedConstants.UserRoles.User];
    public bool IsActive { get; set; } = true;
    public string OwnerStatus { get; set; } = SharedConstants.OwnerStatuses.None;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

