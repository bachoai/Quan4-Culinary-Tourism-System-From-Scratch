using MongoDB.Bson.Serialization.Attributes;

namespace Quan4CulinaryTourism.Api.Models;

[BsonIgnoreExtraElements]
public class User : BaseDocument
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> Roles { get; set; } = [SharedConstants.UserRoles.User];
    public bool IsActive { get; set; } = true;
    public string OwnerStatus { get; set; } = SharedConstants.OwnerNone;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
