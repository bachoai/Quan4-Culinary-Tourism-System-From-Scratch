using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Repositories;

public class PoiLocalizationRepository
{
    private readonly MongoDbContext _context;
    public PoiLocalizationRepository(MongoDbContext context) => _context = context;

    public async Task<PoiLocalization?> GetByPoiAndLangAsync(string poiId, string lang, CancellationToken cancellationToken = default) =>
        await _context.PoiLocalizations.Find(x => x.PoiId == poiId && x.Lang == lang).FirstOrDefaultAsync(cancellationToken);

    public Task<List<PoiLocalization>> GetByPoiIdAsync(string poiId, CancellationToken cancellationToken = default) =>
        _context.PoiLocalizations.Find(x => x.PoiId == poiId).ToListAsync(cancellationToken);

    public async Task CreateAsync(PoiLocalization entity, CancellationToken cancellationToken = default) =>
        await _context.PoiLocalizations.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(PoiLocalization entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.PoiLocalizations.ReplaceOneAsync(x => x.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string poiId, string lang, CancellationToken cancellationToken = default) =>
        await _context.PoiLocalizations.DeleteOneAsync(x => x.PoiId == poiId && x.Lang == lang, cancellationToken);

    public async Task DeleteByPoiIdAsync(string poiId, CancellationToken cancellationToken = default) =>
        await _context.PoiLocalizations.DeleteManyAsync(x => x.PoiId == poiId, cancellationToken);
}
