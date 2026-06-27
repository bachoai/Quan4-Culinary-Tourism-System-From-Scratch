using MongoDB.Bson;
using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Repositories;

public class AnalyticsRepository
{
    private readonly MongoDbContext _context;
    public AnalyticsRepository(MongoDbContext context) => _context = context;

    public async Task CreateAsync(AnalyticsEvent entity, CancellationToken cancellationToken = default) =>
        await _context.AnalyticsEvents.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public Task<long> CountByEventNameAsync(string eventName, CancellationToken cancellationToken = default) =>
        _context.AnalyticsEvents.CountDocumentsAsync(x => x.EventName == eventName, cancellationToken: cancellationToken);

    public Task<long> CountByEventNameAndPoiIdsAsync(string eventName, IEnumerable<string> poiIds, CancellationToken cancellationToken = default) =>
        _context.AnalyticsEvents.CountDocumentsAsync(x => x.EventName == eventName && poiIds.Contains(x.PoiId!), cancellationToken: cancellationToken);

    public async Task<List<TopPoiAnalyticsResponse>> GetTopPoiViewsAsync(CancellationToken cancellationToken = default) =>
        await GetTopByEventAsync("poi_viewed", cancellationToken);

    public async Task<List<TopPoiAnalyticsResponse>> GetTopAudioPlaysAsync(CancellationToken cancellationToken = default) =>
        await GetTopByEventAsync("audio_played", cancellationToken);

    public async Task<PagedResponse<UsageHistoryEntryResponse>> SearchUsageHistoryAsync(UsageHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 10, 200);
        var filter = BuildUsageHistoryFilter(request);

        var totalItems = await _context.AnalyticsEvents.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.AnalyticsEvents.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<UsageHistoryEntryResponse>
        {
            Items = items.Select(item => new UsageHistoryEntryResponse
            {
                Id = item.Id,
                AnonymousId = item.AnonymousId,
                SessionId = item.SessionId,
                PageViewId = item.PageViewId,
                EventName = item.EventName,
                PoiId = item.PoiId,
                Lang = item.Lang,
                Metadata = item.Metadata,
                CreatedAt = item.CreatedAt
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    private async Task<List<TopPoiAnalyticsResponse>> GetTopByEventAsync(string eventName, CancellationToken cancellationToken)
    {
        var results = await _context.AnalyticsEvents.Aggregate()
            .Match(x => x.EventName == eventName && x.PoiId != null)
            .Group(x => x.PoiId, x => new BsonDocument
            {
                { "_id", x.Key },
                { "count", x.Count() }
            })
            .Sort(new BsonDocument("count", -1))
            .Limit(10)
            .ToListAsync(cancellationToken);

        return results.Select(item => new TopPoiAnalyticsResponse
        {
            PoiId = item["_id"].AsString,
            Count = item["count"].ToInt64()
        }).ToList();
    }

    private static FilterDefinition<AnalyticsEvent> BuildUsageHistoryFilter(UsageHistoryRequest request)
    {
        var builder = Builders<AnalyticsEvent>.Filter;
        var filter = FilterDefinition<AnalyticsEvent>.Empty;

        if (!string.IsNullOrWhiteSpace(request.EventName))
        {
            filter &= builder.Eq(x => x.EventName, request.EventName);
        }

        if (!string.IsNullOrWhiteSpace(request.PoiId))
        {
            filter &= builder.Eq(x => x.PoiId, request.PoiId);
        }

        if (!string.IsNullOrWhiteSpace(request.Lang))
        {
            filter &= builder.Eq(x => x.Lang, request.Lang);
        }

        return filter;
    }
}
