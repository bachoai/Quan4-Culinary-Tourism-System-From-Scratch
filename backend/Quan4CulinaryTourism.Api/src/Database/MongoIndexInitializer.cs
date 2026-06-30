using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Database;

public class MongoIndexInitializer
{
    private readonly MongoDbContext _context;

    public MongoIndexInitializer(MongoDbContext context)
    {
        _context = context;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _context.Users.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Email), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Roles)),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.IsActive))
        ], cancellationToken);

        await _context.Categories.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Category>(Builders<Category>.IndexKeys.Ascending(x => x.Code), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Category>(Builders<Category>.IndexKeys.Ascending(x => x.IsActive)),
            new CreateIndexModel<Category>(Builders<Category>.IndexKeys.Ascending(x => x.SortOrder))
        ], cancellationToken);

        await _context.Pois.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Poi>(Builders<Poi>.IndexKeys.Geo2DSphere("Location")),
            new CreateIndexModel<Poi>(Builders<Poi>.IndexKeys.Ascending(x => x.CategoryId)),
            new CreateIndexModel<Poi>(Builders<Poi>.IndexKeys.Ascending(x => x.OwnerId)),
            new CreateIndexModel<Poi>(Builders<Poi>.IndexKeys.Ascending(x => x.IsActive)),
            new CreateIndexModel<Poi>(Builders<Poi>.IndexKeys.Ascending(x => x.PriceRange)),
            new CreateIndexModel<Poi>(Builders<Poi>.IndexKeys.Ascending(x => x.Rating)),
            new CreateIndexModel<Poi>(Builders<Poi>.IndexKeys.Ascending(x => x.UpdatedAt)),
            new CreateIndexModel<Poi>(Builders<Poi>.IndexKeys.Text(x => x.Name).Text(x => x.Description).Text(x => x.Address))
        ], cancellationToken);

        await _context.PoiLocalizations.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PoiLocalization>(
                Builders<PoiLocalization>.IndexKeys.Ascending(x => x.PoiId).Ascending(x => x.Lang),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<PoiLocalization>(Builders<PoiLocalization>.IndexKeys.Ascending(x => x.Lang))
        ], cancellationToken);

        await _context.PoiAudios.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PoiAudio>(
                Builders<PoiAudio>.IndexKeys.Ascending(x => x.PoiId).Ascending(x => x.Lang),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<PoiAudio>(Builders<PoiAudio>.IndexKeys.Ascending(x => x.Status))
        ], cancellationToken);

        await _context.AudioTasks.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AudioTask>(Builders<AudioTask>.IndexKeys.Ascending(x => x.TaskId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<AudioTask>(Builders<AudioTask>.IndexKeys.Ascending(x => x.PoiId)),
            new CreateIndexModel<AudioTask>(Builders<AudioTask>.IndexKeys.Ascending(x => x.Status)),
            new CreateIndexModel<AudioTask>(Builders<AudioTask>.IndexKeys.Ascending(x => x.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
        ], cancellationToken);

        await _context.OwnerRegistrations.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<OwnerRegistration>(Builders<OwnerRegistration>.IndexKeys.Ascending(x => x.UserId)),
            new CreateIndexModel<OwnerRegistration>(Builders<OwnerRegistration>.IndexKeys.Ascending(x => x.Status)),
            new CreateIndexModel<OwnerRegistration>(Builders<OwnerRegistration>.IndexKeys.Ascending(x => x.CreatedAt))
        ], cancellationToken);

        await _context.OwnerSubmissions.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<OwnerSubmission>(Builders<OwnerSubmission>.IndexKeys.Ascending(x => x.OwnerId)),
            new CreateIndexModel<OwnerSubmission>(Builders<OwnerSubmission>.IndexKeys.Ascending(x => x.PoiId)),
            new CreateIndexModel<OwnerSubmission>(Builders<OwnerSubmission>.IndexKeys.Ascending(x => x.Status)),
            new CreateIndexModel<OwnerSubmission>(Builders<OwnerSubmission>.IndexKeys.Ascending(x => x.SubmissionType)),
            new CreateIndexModel<OwnerSubmission>(Builders<OwnerSubmission>.IndexKeys.Ascending(x => x.CreatedAt))
        ], cancellationToken);

        await _context.AnalyticsEvents.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AnalyticsEvent>(Builders<AnalyticsEvent>.IndexKeys.Ascending(x => x.EventName)),
            new CreateIndexModel<AnalyticsEvent>(Builders<AnalyticsEvent>.IndexKeys.Ascending(x => x.PoiId)),
            new CreateIndexModel<AnalyticsEvent>(Builders<AnalyticsEvent>.IndexKeys.Ascending(x => x.CreatedAt)),
            new CreateIndexModel<AnalyticsEvent>(Builders<AnalyticsEvent>.IndexKeys.Ascending(x => x.AnonymousId).Ascending(x => x.SessionId)),
            new CreateIndexModel<AnalyticsEvent>(Builders<AnalyticsEvent>.IndexKeys.Ascending(x => x.EventName).Ascending(x => x.CreatedAt)),
            new CreateIndexModel<AnalyticsEvent>(Builders<AnalyticsEvent>.IndexKeys.Ascending(x => x.SessionId).Ascending(x => x.CreatedAt))
        ], cancellationToken);

        await _context.MediaFiles.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<MediaFile>(Builders<MediaFile>.IndexKeys.Ascending(x => x.FileType)),
            new CreateIndexModel<MediaFile>(Builders<MediaFile>.IndexKeys.Ascending(x => x.UploadedBy)),
            new CreateIndexModel<MediaFile>(Builders<MediaFile>.IndexKeys.Ascending(x => x.CreatedAt))
        ], cancellationToken);

        await _context.MapPacks.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<MapPack>(Builders<MapPack>.IndexKeys.Ascending(x => x.Version), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<MapPack>(Builders<MapPack>.IndexKeys.Ascending(x => x.IsActive))
        ], cancellationToken);

        await _context.Tours.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Tour>(Builders<Tour>.IndexKeys.Ascending(x => x.IsActive)),
            new CreateIndexModel<Tour>(Builders<Tour>.IndexKeys.Ascending(x => x.Lang)),
            new CreateIndexModel<Tour>(Builders<Tour>.IndexKeys.Ascending(x => x.UpdatedAt)),
            new CreateIndexModel<Tour>(Builders<Tour>.IndexKeys.Text(x => x.Title).Text(x => x.Description))
        ], cancellationToken);

        await _context.QrActivations.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<QrActivation>(Builders<QrActivation>.IndexKeys.Ascending(x => x.Code), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<QrActivation>(Builders<QrActivation>.IndexKeys.Ascending(x => x.PoiId)),
            new CreateIndexModel<QrActivation>(Builders<QrActivation>.IndexKeys.Ascending(x => x.StopZone).Ascending(x => x.SortOrder)),
            new CreateIndexModel<QrActivation>(Builders<QrActivation>.IndexKeys.Ascending(x => x.IsActive)),
            new CreateIndexModel<QrActivation>(Builders<QrActivation>.IndexKeys.Ascending(x => x.UpdatedAt))
        ], cancellationToken);
    }
}
