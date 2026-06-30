using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Repositories;

public class OwnerSubmissionRepository
{
    private readonly MongoDbContext _context;
    public OwnerSubmissionRepository(MongoDbContext context) => _context = context;

    public async Task CreateAsync(OwnerSubmission entity, CancellationToken cancellationToken = default) =>
        await _context.OwnerSubmissions.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<OwnerSubmission?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _context.OwnerSubmissions.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public Task<List<OwnerSubmission>> GetByOwnerIdAsync(string ownerId, CancellationToken cancellationToken = default) =>
        _context.OwnerSubmissions.Find(x => x.OwnerId == ownerId).SortByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task<List<OwnerSubmission>> GetByStatusAsync(string? status, CancellationToken cancellationToken = default)
    {
        var filter = string.IsNullOrWhiteSpace(status)
            ? FilterDefinition<OwnerSubmission>.Empty
            : Builders<OwnerSubmission>.Filter.Eq(x => x.Status, status);
        return _context.OwnerSubmissions.Find(filter).SortByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(OwnerSubmission entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.OwnerSubmissions.ReplaceOneAsync(x => x.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public Task<long> CountByOwnerAsync(string ownerId, CancellationToken cancellationToken = default) =>
        _context.OwnerSubmissions.CountDocumentsAsync(x => x.OwnerId == ownerId, cancellationToken: cancellationToken);

    public Task<long> CountPendingAsync(CancellationToken cancellationToken = default) =>
        _context.OwnerSubmissions.CountDocumentsAsync(x => x.Status == SharedConstants.SubmissionStatuses.Pending, cancellationToken: cancellationToken);
}

