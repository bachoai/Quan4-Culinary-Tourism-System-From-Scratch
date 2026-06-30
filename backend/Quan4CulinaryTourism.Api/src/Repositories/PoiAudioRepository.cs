using MongoDB.Bson;
using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Repositories;

public class PoiAudioRepository
{
    private readonly MongoDbContext _context;
    public PoiAudioRepository(MongoDbContext context) => _context = context;

    public async Task<PoiAudio?> GetByPoiAndLangAsync(
        string poiId,
        string lang,
        CancellationToken cancellationToken = default,
        bool includeDeleted = false) =>
        await _context.PoiAudios.Find(x =>
                x.PoiId == poiId &&
                x.Lang == lang &&
                (includeDeleted || !x.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<PoiAudio>> GetByPoiIdAsync(
        string poiId,
        CancellationToken cancellationToken = default,
        bool includeDeleted = false) =>
        _context.PoiAudios.Find(x => x.PoiId == poiId && (includeDeleted || !x.IsDeleted)).ToListAsync(cancellationToken);

    public async Task UpsertAsync(PoiAudio audio, CancellationToken cancellationToken = default)
    {
        var existing = await GetByPoiAndLangAsync(
            audio.PoiId,
            audio.Lang,
            cancellationToken,
            includeDeleted: true);
        if (existing is not null)
        {
            audio.Id = existing.Id;
            if (audio.CreatedAt == default)
            {
                audio.CreatedAt = existing.CreatedAt;
            }
        }
        else if (string.IsNullOrWhiteSpace(audio.Id))
        {
            audio.Id = ObjectId.GenerateNewId().ToString();
        }

        audio.UpdatedAt = DateTime.UtcNow;
        await _context.PoiAudios.ReplaceOneAsync(
            x => x.PoiId == audio.PoiId && x.Lang == audio.Lang,
            audio,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task DeleteAsync(string poiId, string lang, CancellationToken cancellationToken = default) =>
        await _context.PoiAudios.DeleteOneAsync(x => x.PoiId == poiId && x.Lang == lang, cancellationToken);
}
