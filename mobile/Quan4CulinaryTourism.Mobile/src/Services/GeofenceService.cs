using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.Devices.Sensors;
using Quan4CulinaryTourism.Mobile.DTOs;
using Quan4CulinaryTourism.Mobile.Models;

namespace Quan4CulinaryTourism.Mobile.Services;

public class GeofenceService
{
    private static readonly TimeSpan PoiCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TriggerCooldown = TimeSpan.FromMinutes(5);

    private readonly PoiApiService _poiApiService;
    private readonly AudioApiService _audioApiService;
    private readonly AudioPlayerService _audioPlayerService;
    private readonly OfflineDatabaseService _offlineDatabaseService;
    private readonly LocationTrackingService _locationTrackingService;
    private readonly SettingsService _settingsService;
    private readonly AnalyticsApiService _analyticsApiService;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private readonly Dictionary<string, DateTime> _cooldownMap = [];
    private List<PoiResponse> _cachedPois = [];
    private DateTime _cachedPoisAtUtc = DateTime.MinValue;
    private string _cachedLanguage = string.Empty;
    private bool _started;

    public GeofenceService(
        PoiApiService poiApiService,
        AudioApiService audioApiService,
        AudioPlayerService audioPlayerService,
        OfflineDatabaseService offlineDatabaseService,
        LocationTrackingService locationTrackingService,
        SettingsService settingsService,
        AnalyticsApiService analyticsApiService)
    {
        _poiApiService = poiApiService;
        _audioApiService = audioApiService;
        _audioPlayerService = audioPlayerService;
        _offlineDatabaseService = offlineDatabaseService;
        _locationTrackingService = locationTrackingService;
        _settingsService = settingsService;
        _analyticsApiService = analyticsApiService;
    }

    public void EnsureStarted()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _locationTrackingService.LocationChanged += OnLocationChanged;
        _locationTrackingService.EnsureStarted();
    }

    private void OnLocationChanged(object? sender, LocationTrackingSample sample)
    {
        _ = HandleLocationAsync(sample);
    }

    private async Task HandleLocationAsync(LocationTrackingSample sample)
    {
        if (!_settingsService.GetAutoNarrationEnabled())
        {
            return;
        }

        if (!await _processingLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var language = _settingsService.GetLanguage();
            var pois = await GetTrackedPoisAsync(language);
            var defaultRadius = _settingsService.GetNarrationRadiusMeters();
            var candidate = pois
                .Where(static poi => poi.Latitude != 0 && poi.Longitude != 0 && poi.AutoNarrationEnabled)
                .Select(poi => new
                {
                    Poi = poi,
                    Distance = Location.CalculateDistance(sample.Location, new Location(poi.Latitude, poi.Longitude), DistanceUnits.Kilometers) * 1000d,
                    Radius = poi.GeofenceRadiusMeters > 0 ? poi.GeofenceRadiusMeters : defaultRadius
                })
                .Where(item => item.Distance <= item.Radius)
                .OrderByDescending(item => item.Poi.Priority)
                .ThenBy(item => item.Distance)
                .FirstOrDefault();

            if (candidate is null)
            {
                return;
            }

            if (_cooldownMap.TryGetValue(candidate.Poi.Id, out var lastTrigger) &&
                DateTime.UtcNow - lastTrigger < TriggerCooldown)
            {
                return;
            }

            var audio = await _audioApiService.GetPoiAudioAsync(candidate.Poi.Id, language)
                ?? await _offlineDatabaseService.GetPoiAudioAsync(candidate.Poi.Id, language);

            var queued = false;
            if (!string.IsNullOrWhiteSpace(audio?.AudioUrl) || !string.IsNullOrWhiteSpace(audio?.LocalAudioPath))
            {
                queued = await _audioPlayerService.QueuePoiAudioAsync(
                    candidate.Poi.Id,
                    language,
                    audio!.AudioUrl,
                    audio.LocalAudioPath,
                    candidate.Poi.Name,
                    "geofence");
            }
            else if (!string.IsNullOrWhiteSpace(candidate.Poi.NarrationText))
            {
                queued = await _audioPlayerService.QueuePoiTtsAsync(
                    candidate.Poi.Id,
                    language,
                    candidate.Poi.NarrationText,
                    candidate.Poi.Name,
                    "geofence");
            }

            if (!queued)
            {
                return;
            }

            _cooldownMap[candidate.Poi.Id] = DateTime.UtcNow;

            await _analyticsApiService.CollectAsync(new CollectAnalyticsRequest
            {
                EventName = "geofence_triggered",
                PoiId = candidate.Poi.Id,
                Lang = language,
                Metadata =
                {
                    ["distanceMeters"] = Math.Round(candidate.Distance),
                    ["radiusMeters"] = candidate.Radius,
                    ["trackingSource"] = sample.Source,
                    ["background"] = sample.IsBackground
                }
            });

            if (!sample.IsBackground)
            {
                await Toast.Make($"Da them thuyet minh vao hang cho: {candidate.Poi.Name}").Show();
            }
        }
        catch
        {
            // Keep location-triggered playback failures isolated.
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task<List<PoiResponse>> GetTrackedPoisAsync(string language)
    {
        var shouldRefresh = _cachedPois.Count == 0
            || !string.Equals(_cachedLanguage, language, StringComparison.Ordinal)
            || DateTime.UtcNow - _cachedPoisAtUtc > PoiCacheDuration;

        if (!shouldRefresh)
        {
            return _cachedPois;
        }

        var pois = await _poiApiService.LoadAllAsync(language, null, null, null);
        if (pois.Count > 0)
        {
            _cachedPois = pois;
            _cachedLanguage = language;
            _cachedPoisAtUtc = DateTime.UtcNow;
            await _offlineDatabaseService.SavePoisAsync(pois);
            return _cachedPois;
        }

        var offlinePois = await _offlineDatabaseService.GetPoisAsync();
        if (offlinePois.Count > 0)
        {
            _cachedPois = offlinePois;
            _cachedLanguage = language;
            _cachedPoisAtUtc = DateTime.UtcNow;
        }

        return _cachedPois;
    }
}
