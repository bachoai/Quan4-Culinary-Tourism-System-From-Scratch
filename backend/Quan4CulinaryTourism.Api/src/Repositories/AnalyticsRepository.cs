using MongoDB.Bson;
using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Repositories;

public class AnalyticsRepository
{
    private static readonly string[] AudioEventNames = ["audio_played", "tts_played"];
    private static readonly string[] OwnerTrackedEventNames = ["poi_viewed", "audio_played", "tts_played", "qr_scanned"];

    private readonly MongoDbContext _context;
    public AnalyticsRepository(MongoDbContext context) => _context = context;

    public async Task CreateAsync(AnalyticsEvent entity, CancellationToken cancellationToken = default) =>
        await _context.AnalyticsEvents.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public Task<long> CountByEventNameAsync(string eventName, CancellationToken cancellationToken = default) =>
        _context.AnalyticsEvents.CountDocumentsAsync(x => x.EventName == eventName, cancellationToken: cancellationToken);

    public Task<long> CountByEventNamesAsync(IEnumerable<string> eventNames, CancellationToken cancellationToken = default)
    {
        var names = eventNames.Distinct(StringComparer.Ordinal).ToList();
        return _context.AnalyticsEvents.CountDocumentsAsync(x => names.Contains(x.EventName), cancellationToken: cancellationToken);
    }

    public Task<long> CountByEventNameAndPoiIdsAsync(string eventName, IEnumerable<string> poiIds, CancellationToken cancellationToken = default) =>
        _context.AnalyticsEvents.CountDocumentsAsync(x => x.EventName == eventName && poiIds.Contains(x.PoiId!), cancellationToken: cancellationToken);

    public Task<long> CountByEventNamesAndPoiIdsAsync(IEnumerable<string> eventNames, IEnumerable<string> poiIds, CancellationToken cancellationToken = default)
    {
        var names = eventNames.Distinct(StringComparer.Ordinal).ToList();
        var poiIdList = poiIds.Distinct(StringComparer.Ordinal).ToList();
        return _context.AnalyticsEvents.CountDocumentsAsync(
            x => names.Contains(x.EventName) && x.PoiId != null && poiIdList.Contains(x.PoiId),
            cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, OwnerPoiEngagementResponse>> GetPoiEngagementStatsAsync(
        IEnumerable<string> poiIds,
        CancellationToken cancellationToken = default)
    {
        var poiIdList = poiIds
            .Where(static poiId => !string.IsNullOrWhiteSpace(poiId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (poiIdList.Count == 0)
        {
            return new Dictionary<string, OwnerPoiEngagementResponse>(StringComparer.Ordinal);
        }

        var results = await _context.AnalyticsEvents.Aggregate()
            .Match(new BsonDocument
            {
                { "PoiId", new BsonDocument("$in", new BsonArray(poiIdList)) },
                { "EventName", new BsonDocument("$in", new BsonArray(OwnerTrackedEventNames)) }
            })
            .Group(new BsonDocument
            {
                { "_id", "$PoiId" },
                { "viewCount", CreateConditionalCountExpression(CreateEventEqualsExpression("poi_viewed")) },
                { "visitorKeys", CreateTrackedUserSetExpression(CreateEventEqualsExpression("poi_viewed")) },
                { "audioPlayCount", CreateConditionalCountExpression(CreateEventInExpression(AudioEventNames)) },
                { "audioListenerKeys", CreateTrackedUserSetExpression(CreateEventInExpression(AudioEventNames)) },
                { "qrScanCount", CreateConditionalCountExpression(CreateEventEqualsExpression("qr_scanned")) }
            })
            .Project(new BsonDocument
            {
                { "_id", 1 },
                { "viewCount", 1 },
                { "audioPlayCount", 1 },
                { "qrScanCount", 1 },
                { "uniqueVisitorCount", CreateDistinctTrackedUserCountExpression("$visitorKeys") },
                { "uniqueAudioListenerCount", CreateDistinctTrackedUserCountExpression("$audioListenerKeys") }
            })
            .ToListAsync(cancellationToken);

        return results.ToDictionary(
            item => item["_id"].AsString,
            item => new OwnerPoiEngagementResponse
            {
                PoiId = item["_id"].AsString,
                ViewCount = item["viewCount"].ToInt64(),
                UniqueVisitorCount = item["uniqueVisitorCount"].ToInt64(),
                AudioPlayCount = item["audioPlayCount"].ToInt64(),
                UniqueAudioListenerCount = item["uniqueAudioListenerCount"].ToInt64(),
                QrScanCount = item["qrScanCount"].ToInt64()
            },
            StringComparer.Ordinal);
    }

    public async Task<OwnerPortfolioEngagementResponse> GetPortfolioEngagementStatsAsync(
        IEnumerable<string> poiIds,
        CancellationToken cancellationToken = default)
    {
        var poiIdList = poiIds
            .Where(static poiId => !string.IsNullOrWhiteSpace(poiId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (poiIdList.Count == 0)
        {
            return new OwnerPortfolioEngagementResponse();
        }

        var result = await _context.AnalyticsEvents.Aggregate()
            .Match(new BsonDocument
            {
                { "PoiId", new BsonDocument("$in", new BsonArray(poiIdList)) },
                { "EventName", new BsonDocument("$in", new BsonArray(OwnerTrackedEventNames)) }
            })
            .Group(new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "viewCount", CreateConditionalCountExpression(CreateEventEqualsExpression("poi_viewed")) },
                { "visitorKeys", CreateTrackedUserSetExpression(CreateEventEqualsExpression("poi_viewed")) },
                { "audioPlayCount", CreateConditionalCountExpression(CreateEventInExpression(AudioEventNames)) },
                { "audioListenerKeys", CreateTrackedUserSetExpression(CreateEventInExpression(AudioEventNames)) },
                { "qrScanCount", CreateConditionalCountExpression(CreateEventEqualsExpression("qr_scanned")) }
            })
            .Project(new BsonDocument
            {
                { "_id", 0 },
                { "viewCount", 1 },
                { "audioPlayCount", 1 },
                { "qrScanCount", 1 },
                { "uniqueVisitorCount", CreateDistinctTrackedUserCountExpression("$visitorKeys") },
                { "uniqueAudioListenerCount", CreateDistinctTrackedUserCountExpression("$audioListenerKeys") }
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            return new OwnerPortfolioEngagementResponse();
        }

        return new OwnerPortfolioEngagementResponse
        {
            ViewCount = result["viewCount"].ToInt64(),
            UniqueVisitorCount = result["uniqueVisitorCount"].ToInt64(),
            AudioPlayCount = result["audioPlayCount"].ToInt64(),
            UniqueAudioListenerCount = result["uniqueAudioListenerCount"].ToInt64(),
            QrScanCount = result["qrScanCount"].ToInt64()
        };
    }

    public async Task<List<TopPoiAnalyticsResponse>> GetTopPoiViewsAsync(CancellationToken cancellationToken = default) =>
        await GetTopByEventAsync("poi_viewed", cancellationToken);

    public async Task<List<TopPoiAnalyticsResponse>> GetTopAudioPlaysAsync(CancellationToken cancellationToken = default) =>
        await GetTopByEventsAsync(AudioEventNames, cancellationToken);

    public async Task<double> GetAverageListenDurationSecondsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _context.AnalyticsEvents.Find(x => x.ListenDurationSeconds != null && x.ListenDurationSeconds > 0)
            .Limit(2000)
            .ToListAsync(cancellationToken);

        return items.Count == 0
            ? 0
            : Math.Round(items.Average(x => x.ListenDurationSeconds ?? 0), 1);
    }

    public async Task<List<AnalyticsHeatmapPointResponse>> GetHeatmapPointsAsync(int maxPoints = 80, CancellationToken cancellationToken = default)
    {
        var items = await _context.AnalyticsEvents.Find(x => x.EventName == "location_sample" && x.Latitude != null && x.Longitude != null)
            .SortByDescending(x => x.CreatedAt)
            .Limit(2000)
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(
                item => new
                {
                    Latitude = Math.Round(item.Latitude ?? 0, 4),
                    Longitude = Math.Round(item.Longitude ?? 0, 4)
                })
            .Select(group => new AnalyticsHeatmapPointResponse
            {
                Latitude = group.Key.Latitude,
                Longitude = group.Key.Longitude,
                Count = group.LongCount(),
                LastSeenAt = group.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.LastSeenAt)
            .Take(maxPoints)
            .ToList();
    }

    public async Task<List<AnalyticsRouteTraceResponse>> GetRecentRouteTracesAsync(int maxRoutes = 6, int maxPointsPerRoute = 25, CancellationToken cancellationToken = default)
    {
        var items = await _context.AnalyticsEvents.Find(x =>
                x.EventName == "location_sample"
                && x.Latitude != null
                && x.Longitude != null
                && x.AnonymousId != null)
            .SortByDescending(x => x.CreatedAt)
            .Limit(1500)
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(x => $"{x.AnonymousId}:{x.SessionId}")
            .Select(group =>
            {
                var orderedPoints = group.OrderBy(x => x.CreatedAt).ToList();
                var trimmedPoints = orderedPoints.Count > maxPointsPerRoute
                    ? orderedPoints.Skip(orderedPoints.Count - maxPointsPerRoute).ToList()
                    : orderedPoints;

                return new AnalyticsRouteTraceResponse
                {
                    AnonymousId = group.First().AnonymousId ?? string.Empty,
                    SessionId = group.First().SessionId,
                    PointCount = orderedPoints.Count,
                    StartedAt = orderedPoints.First().CreatedAt,
                    EndedAt = orderedPoints.Last().CreatedAt,
                    Points = trimmedPoints.Select(point => new AnalyticsRoutePointResponse
                    {
                        Latitude = point.Latitude ?? 0,
                        Longitude = point.Longitude ?? 0,
                        IsBackground = point.IsBackground ?? false,
                        Source = point.TrackingSource ?? "unknown",
                        CreatedAt = point.CreatedAt
                    }).ToList()
                };
            })
            .Where(trace => trace.PointCount > 1)
            .OrderByDescending(trace => trace.EndedAt)
            .Take(maxRoutes)
            .ToList();
    }

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

    private async Task<List<TopPoiAnalyticsResponse>> GetTopByEventAsync(string eventName, CancellationToken cancellationToken) =>
        await GetTopByEventsAsync([eventName], cancellationToken);

    private async Task<List<TopPoiAnalyticsResponse>> GetTopByEventsAsync(IEnumerable<string> eventNames, CancellationToken cancellationToken)
    {
        var names = eventNames.Distinct(StringComparer.Ordinal).ToList();
        var results = await _context.AnalyticsEvents.Aggregate()
            .Match(x => names.Contains(x.EventName) && x.PoiId != null)
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

    private static BsonDocument CreateEventEqualsExpression(string eventName) =>
        new("$eq", new BsonArray { "$EventName", eventName });

    private static BsonDocument CreateEventInExpression(IEnumerable<string> eventNames) =>
        new("$in", new BsonArray { "$EventName", new BsonArray(eventNames) });

    private static BsonDocument CreateConditionalCountExpression(BsonValue condition) =>
        new("$sum", new BsonDocument("$cond", new BsonArray { condition, 1, 0 }));

    private static BsonDocument CreateTrackedUserSetExpression(BsonValue condition) =>
        new("$addToSet", new BsonDocument("$cond", new BsonArray
        {
            condition,
            CreateTrackedUserKeyExpression(),
            BsonNull.Value
        }));

    private static BsonDocument CreateTrackedUserKeyExpression() =>
        new("$let", new BsonDocument
        {
            {
                "vars",
                new BsonDocument
                {
                    { "anonymousId", new BsonDocument("$ifNull", new BsonArray { "$AnonymousId", string.Empty }) },
                    { "sessionId", new BsonDocument("$ifNull", new BsonArray { "$SessionId", string.Empty }) }
                }
            },
            {
                "in",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$ne", new BsonArray { "$$anonymousId", string.Empty }),
                    "$$anonymousId",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$ne", new BsonArray { "$$sessionId", string.Empty }),
                        "$$sessionId",
                        BsonNull.Value
                    })
                })
            }
        });

    private static BsonDocument CreateDistinctTrackedUserCountExpression(string inputArrayField) =>
        new("$size", new BsonDocument("$filter", new BsonDocument
        {
            { "input", inputArrayField },
            { "as", "userKey" },
            {
                "cond",
                new BsonDocument("$and", new BsonArray
                {
                    new BsonDocument("$ne", new BsonArray { "$$userKey", BsonNull.Value }),
                    new BsonDocument("$ne", new BsonArray { "$$userKey", string.Empty })
                })
            }
        }));
}
