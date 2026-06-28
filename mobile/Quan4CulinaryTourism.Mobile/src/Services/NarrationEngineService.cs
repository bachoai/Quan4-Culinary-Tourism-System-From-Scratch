using Quan4CulinaryTourism.Mobile.DTOs;
using Quan4CulinaryTourism.Mobile.Models;

namespace Quan4CulinaryTourism.Mobile.Services;

public sealed class NarrationEngineService
{
    private static readonly TimeSpan TriggerCooldown = TimeSpan.FromMinutes(5);

    private readonly AudioApiService _audioApiService;
    private readonly AudioPlayerService _audioPlayerService;
    private readonly OfflineDatabaseService _offlineDatabaseService;
    private readonly AnalyticsApiService _analyticsApiService;
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, DateTime> _cooldownMap = [];

    public NarrationEngineService(
        AudioApiService audioApiService,
        AudioPlayerService audioPlayerService,
        OfflineDatabaseService offlineDatabaseService,
        AnalyticsApiService analyticsApiService)
    {
        _audioApiService = audioApiService;
        _audioPlayerService = audioPlayerService;
        _offlineDatabaseService = offlineDatabaseService;
        _analyticsApiService = analyticsApiService;
    }

    public async Task<bool> TryQueueAutoNarrationAsync(
        PoiResponse poi,
        string language,
        LocationTrackingSample sample,
        double distanceMeters,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poi.Id))
        {
            return false;
        }

        if (IsInCooldown(poi.Id))
        {
            return false;
        }

        // Keep auto-narration focused on the highest-priority live POI instead of stacking a long queue.
        if (_audioPlayerService.HasActiveOrPendingPlayback)
        {
            return false;
        }

        var audio = await _audioApiService.GetPoiAudioAsync(poi.Id, language)
            ?? await _offlineDatabaseService.GetPoiAudioAsync(poi.Id, language);

        var queued = false;
        if (!string.IsNullOrWhiteSpace(poi.NarrationText))
        {
            queued = await _audioPlayerService.QueuePoiTtsAsync(
                poi.Id,
                language,
                poi.NarrationText,
                poi.Name,
                "geofence");
        }
        else if (!string.IsNullOrWhiteSpace(audio?.AudioUrl) || !string.IsNullOrWhiteSpace(audio?.LocalAudioPath))
        {
            queued = await _audioPlayerService.QueuePoiAudioAsync(
                poi.Id,
                language,
                audio!.AudioUrl,
                audio.LocalAudioPath,
                poi.Name,
                "geofence");
        }

        if (!queued)
        {
            return false;
        }

        MarkTriggered(poi.Id);
        await _analyticsApiService.CollectAsync(new CollectAnalyticsRequest
        {
            EventName = "geofence_triggered",
            PoiId = poi.Id,
            Lang = language,
            Metadata =
            {
                ["distanceMeters"] = Math.Round(distanceMeters),
                ["radiusMeters"] = radiusMeters,
                ["trackingSource"] = sample.Source,
                ["background"] = sample.IsBackground
            }
        });

        return true;
    }

    public bool IsInCooldown(string poiId)
    {
        lock (_syncRoot)
        {
            return _cooldownMap.TryGetValue(poiId, out var lastTrigger)
                && DateTime.UtcNow - lastTrigger < TriggerCooldown;
        }
    }

    private void MarkTriggered(string poiId)
    {
        lock (_syncRoot)
        {
            _cooldownMap[poiId] = DateTime.UtcNow;
        }
    }
}
