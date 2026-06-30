using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Quan4CulinaryTourism.Api.Database;

public class DeploymentDatabaseResetService
{
    private readonly MongoDbContext _context;
    private readonly DatabaseResetSettings _settings;
    private readonly ILogger<DeploymentDatabaseResetService> _logger;

    public DeploymentDatabaseResetService(
        MongoDbContext context,
        IOptions<DatabaseResetSettings> settings,
        ILogger<DeploymentDatabaseResetService> logger)
    {
        _context = context;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ResetIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var token = _settings.Token.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Database reset was enabled but no token was provided. Skipping reset.");
            return;
        }

        var markerCollectionName = string.IsNullOrWhiteSpace(_settings.MarkerCollectionName)
            ? "__deployment_resets"
            : _settings.MarkerCollectionName.Trim();
        var markerId = $"deploy-reset:{token}";
        var markers = _context.Database.GetCollection<BsonDocument>(markerCollectionName);
        var existingMarker = await markers
            .Find(Builders<BsonDocument>.Filter.Eq("_id", markerId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingMarker is not null)
        {
            _logger.LogInformation("Database reset token {Token} already executed. Skipping reset.", token);
            return;
        }

        var databaseName = _context.Database.DatabaseNamespace.DatabaseName;
        _logger.LogWarning(
            "Running one-time deployment database reset for token {Token} on database {DatabaseName}.",
            token,
            databaseName);

        await _context.Database.Client.DropDatabaseAsync(databaseName, cancellationToken);

        var database = _context.Database.Client.GetDatabase(databaseName);
        var resetMarkers = database.GetCollection<BsonDocument>(markerCollectionName);
        await resetMarkers.InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = markerId,
                ["token"] = token,
                ["executedAtUtc"] = DateTime.UtcNow,
                ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty
            },
            cancellationToken: cancellationToken);

        _logger.LogWarning("One-time deployment database reset completed for token {Token}.", token);
    }
}
