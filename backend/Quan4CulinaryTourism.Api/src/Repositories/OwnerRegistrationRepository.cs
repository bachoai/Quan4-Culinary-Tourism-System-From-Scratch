using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Repositories;

public class OwnerRegistrationRepository
{
    private readonly MongoDbContext _context;
    public OwnerRegistrationRepository(MongoDbContext context) => _context = context;

    public async Task CreateAsync(OwnerRegistration entity, CancellationToken cancellationToken = default) =>
        await _context.OwnerRegistrations.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<OwnerRegistration?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _context.OwnerRegistrations.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<OwnerRegistration?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
        await _context.OwnerRegistrations.Find(x => x.UserId == userId).SortByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public async Task<OwnerRegistration?> GetLatestByUserIdAndStatusAsync(string userId, string status, CancellationToken cancellationToken = default) =>
        await _context.OwnerRegistrations
            .Find(x => x.UserId == userId && x.Status == status)
            .SortByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<OwnerRegistration>> GetByStatusAsync(string? status, CancellationToken cancellationToken = default)
    {
        var filter = string.IsNullOrWhiteSpace(status)
            ? FilterDefinition<OwnerRegistration>.Empty
            : Builders<OwnerRegistration>.Filter.Eq(x => x.Status, status);
        return _context.OwnerRegistrations.Find(filter).SortByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(OwnerRegistration entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.OwnerRegistrations.ReplaceOneAsync(x => x.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _context.OwnerRegistrations.DeleteOneAsync(x => x.Id == id, cancellationToken);

    public Task<long> CountPendingAsync(CancellationToken cancellationToken = default) =>
        _context.OwnerRegistrations.CountDocumentsAsync(x => x.Status == SharedConstants.OwnerStatuses.Pending, cancellationToken: cancellationToken);
}

