using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Repositories;

public class TourRepository
{
    private readonly MongoDbContext _context;

    public TourRepository(MongoDbContext context) => _context = context;

    public Task<List<Tour>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _context.Tours.Find(FilterDefinition<Tour>.Empty).SortByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);

    public Task<List<Tour>> GetActiveAsync(string? lang = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Tour>.Filter.Eq(x => x.IsActive, true);
        if (!string.IsNullOrWhiteSpace(lang))
        {
            filter &= Builders<Tour>.Filter.Eq(x => x.Lang, lang);
        }

        return _context.Tours.Find(filter).SortBy(x => x.Title).ToListAsync(cancellationToken);
    }

    public async Task<Tour?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _context.Tours.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public Task CreateAsync(Tour entity, CancellationToken cancellationToken = default) =>
        _context.Tours.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public Task UpdateAsync(Tour entity, CancellationToken cancellationToken = default) =>
        _context.Tours.ReplaceOneAsync(x => x.Id == entity.Id, entity, cancellationToken: cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _context.Tours.DeleteOneAsync(x => x.Id == id, cancellationToken);
}
