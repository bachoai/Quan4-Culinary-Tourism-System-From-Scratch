using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Database;

public class MongoDbContext
{
    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var mongoClient = new MongoClient(settings.Value.ConnectionString);
        Database = mongoClient.GetDatabase(settings.Value.DatabaseName);
        Users = Database.GetCollection<User>("users");
        Roles = Database.GetCollection<Role>("roles");
        Categories = Database.GetCollection<Category>("categories");
        Pois = Database.GetCollection<Poi>("pois");
        PoiLocalizations = Database.GetCollection<PoiLocalization>("poi_localizations");
        PoiAudios = Database.GetCollection<PoiAudio>("poi_audios");
        AudioTasks = Database.GetCollection<AudioTask>("audio_tasks");
        OwnerRegistrations = Database.GetCollection<OwnerRegistration>("owner_registrations");
        OwnerSubmissions = Database.GetCollection<OwnerSubmission>("owner_submissions");
        AnalyticsEvents = Database.GetCollection<AnalyticsEvent>("analytics_events");
        AuditLogs = Database.GetCollection<AuditLog>("audit_logs");
        MediaFiles = Database.GetCollection<MediaFile>("media_files");
        MapPacks = Database.GetCollection<MapPack>("map_packs");
        Tours = Database.GetCollection<Tour>("tours");
    }

    public IMongoDatabase Database { get; }
    public IMongoCollection<User> Users { get; }
    public IMongoCollection<Role> Roles { get; }
    public IMongoCollection<Category> Categories { get; }
    public IMongoCollection<Poi> Pois { get; }
    public IMongoCollection<PoiLocalization> PoiLocalizations { get; }
    public IMongoCollection<PoiAudio> PoiAudios { get; }
    public IMongoCollection<AudioTask> AudioTasks { get; }
    public IMongoCollection<OwnerRegistration> OwnerRegistrations { get; }
    public IMongoCollection<OwnerSubmission> OwnerSubmissions { get; }
    public IMongoCollection<AnalyticsEvent> AnalyticsEvents { get; }
    public IMongoCollection<AuditLog> AuditLogs { get; }
    public IMongoCollection<MediaFile> MediaFiles { get; }
    public IMongoCollection<MapPack> MapPacks { get; }
    public IMongoCollection<Tour> Tours { get; }
}
