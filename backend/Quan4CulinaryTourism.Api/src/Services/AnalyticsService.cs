using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;
using System.Text.Json;

namespace Quan4CulinaryTourism.Api.Services;

public class AnalyticsService
{
    private readonly AnalyticsRepository _analyticsRepository;

    public AnalyticsService(AnalyticsRepository analyticsRepository)
    {
        _analyticsRepository = analyticsRepository;
    }

    public async Task CollectAsync(CollectAnalyticsRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new AnalyticsEvent
        {
            EventName = request.EventName,
            AnonymousId = request.AnonymousId,
            SessionId = request.SessionId,
            PageViewId = request.PageViewId,
            PoiId = request.PoiId,
            Lang = request.Lang,
            Metadata = NormalizeMetadata(request.Metadata)
        };
        await _analyticsRepository.CreateAsync(entity, cancellationToken);
    }

    public async Task<AnalyticsSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        return new AnalyticsSummaryResponse
        {
            PoiViewedCount = await _analyticsRepository.CountByEventNameAsync("poi_viewed", cancellationToken),
            AudioPlayedCount = await _analyticsRepository.CountByEventNameAsync("audio_played", cancellationToken),
            SearchExecutedCount = await _analyticsRepository.CountByEventNameAsync("search_executed", cancellationToken),
            TopPoiViews = await _analyticsRepository.GetTopPoiViewsAsync(cancellationToken),
            TopPoiAudioPlays = await _analyticsRepository.GetTopAudioPlaysAsync(cancellationToken)
        };
    }

    public Task<PagedResponse<UsageHistoryEntryResponse>> GetUsageHistoryAsync(UsageHistoryRequest request, CancellationToken cancellationToken = default) =>
        _analyticsRepository.SearchUsageHistoryAsync(request, cancellationToken);

    private static Dictionary<string, object> NormalizeMetadata(Dictionary<string, object> metadata)
    {
        var normalized = new Dictionary<string, object>();
        foreach (var entry in metadata)
        {
            normalized[entry.Key] = NormalizeValue(entry.Value);
        }

        return normalized;
    }

    private static object NormalizeValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            JsonElement json => NormalizeJsonElement(json),
            Dictionary<string, object> dict => NormalizeMetadata(dict),
            IEnumerable<object> list => list.Select(NormalizeValue).ToList(),
            _ => value
        };
    }

    private static object NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(prop => prop.Name, prop => NormalizeJsonElement(prop.Value)),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.ToString()
        };
    }
}
