using MongoDB.Driver;
using System.Text.RegularExpressions;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Repositories;

public class PoiRepository
{
    private readonly MongoDbContext _context;
    public PoiRepository(MongoDbContext context) => _context = context;

    public Task<List<Poi>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _context.Pois.Find(FilterDefinition<Poi>.Empty).SortByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);

    public Task<List<Poi>> GetPublicPoisAsync(CancellationToken cancellationToken = default) =>
        _context.Pois.Find(x => x.IsActive).SortByDescending(x => x.Priority).ThenByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);

    public async Task<Poi?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _context.Pois.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<List<Poi>> GetManyByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var normalizedIds = ids.Where(static id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        return await _context.Pois.Find(x => normalizedIds.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<List<Poi>> GetPublicManyByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var normalizedIds = ids.Where(static id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        return await _context.Pois.Find(x => normalizedIds.Contains(x.Id) && x.IsActive).ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(Poi poi, CancellationToken cancellationToken = default) =>
        await _context.Pois.InsertOneAsync(poi, cancellationToken: cancellationToken);

    public async Task DeleteHardAsync(string id, CancellationToken cancellationToken = default) =>
        await _context.Pois.DeleteOneAsync(x => x.Id == id, cancellationToken);

    public async Task UpdateAsync(Poi poi, CancellationToken cancellationToken = default)
    {
        poi.UpdatedAt = DateTime.UtcNow;
        await _context.Pois.ReplaceOneAsync(x => x.Id == poi.Id, poi, cancellationToken: cancellationToken);
    }

    public async Task SoftDeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var update = Builders<Poi>.Update.Set(x => x.IsActive, false).Set(x => x.UpdatedAt, DateTime.UtcNow);
        await _context.Pois.UpdateOneAsync(x => x.Id == id, update, cancellationToken: cancellationToken);
    }

    public async Task SetActiveAsync(string id, bool isActive, CancellationToken cancellationToken = default)
    {
        var update = Builders<Poi>.Update.Set(x => x.IsActive, isActive).Set(x => x.UpdatedAt, DateTime.UtcNow);
        await _context.Pois.UpdateOneAsync(x => x.Id == id, update, cancellationToken: cancellationToken);
    }

    public Task<List<Poi>> GetByOwnerIdAsync(string ownerId, CancellationToken cancellationToken = default) =>
        _context.Pois.Find(x => x.OwnerId == ownerId).ToListAsync(cancellationToken);

    public Task<List<Poi>> GetPublicFilteredAsync(PoiSearchRequest request, CancellationToken cancellationToken = default) =>
        _context.Pois.Find(BuildSearchFilter(request, true))
            .SortByDescending(x => x.Priority)
            .ThenByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

    public async Task<List<Poi>> SearchAsync(PoiSearchRequest request, bool publicOnly = true, CancellationToken cancellationToken = default)
    {
        var filter = BuildSearchFilter(request, publicOnly);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        return await _context.Pois.Find(filter)
            .SortByDescending(x => x.Priority)
            .ThenByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<long> CountAsync(FilterDefinition<Poi>? filter = null, CancellationToken cancellationToken = default) =>
        _context.Pois.CountDocumentsAsync(filter ?? FilterDefinition<Poi>.Empty, cancellationToken: cancellationToken);

    private static FilterDefinition<Poi> BuildSearchFilter(PoiSearchRequest request, bool publicOnly)
    {
        var filter = Builders<Poi>.Filter.Empty;
        if (publicOnly)
        {
            filter &= Builders<Poi>.Filter.Eq(x => x.IsActive, true);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var escapedKeyword = Regex.Escape(request.Keyword.Trim());
            filter &= Builders<Poi>.Filter.Or(
                Builders<Poi>.Filter.Regex(x => x.Name, new MongoDB.Bson.BsonRegularExpression(escapedKeyword, "i")),
                Builders<Poi>.Filter.Regex(x => x.Description, new MongoDB.Bson.BsonRegularExpression(escapedKeyword, "i")),
                Builders<Poi>.Filter.Regex(x => x.Address, new MongoDB.Bson.BsonRegularExpression(escapedKeyword, "i")));
        }

        if (!string.IsNullOrWhiteSpace(request.CategoryId))
        {
            filter &= Builders<Poi>.Filter.Eq(x => x.CategoryId, request.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.PriceRange))
        {
            filter &= Builders<Poi>.Filter.Eq(x => x.PriceRange, request.PriceRange);
        }

        return filter;
    }
}
