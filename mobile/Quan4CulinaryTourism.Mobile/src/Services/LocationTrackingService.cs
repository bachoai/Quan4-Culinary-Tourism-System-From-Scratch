using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace Quan4CulinaryTourism.Mobile.Services;

public sealed partial class LocationTrackingService
{
    private static readonly TimeSpan MaxSampleAge = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinSampleInterval = TimeSpan.FromSeconds(6);
    private const double MaxAcceptedAccuracyMeters = 80;
    private const double MinAcceptedMovementMeters = 12;

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SettingsService _settingsService;
    private LocationTrackingSample? _lastAcceptedSample;
    private bool _isRunning;

    public LocationTrackingService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.AutoNarrationSettingsChanged += OnAutoNarrationSettingsChanged;
    }

    public event EventHandler<LocationTrackingSample>? LocationChanged;

    public bool IsRunning => _isRunning;

    public bool SupportsBackgroundTracking => PlatformSupportsBackgroundTracking();

    public Location? LastKnownLocation => _lastAcceptedSample?.Location;

    public DateTimeOffset? LastKnownTimestampUtc => _lastAcceptedSample?.TimestampUtc;

    public void EnsureStarted() => _ = EnsureStartedAsync();

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (!_settingsService.GetAutoNarrationEnabled())
            {
                if (_isRunning)
                {
                    await StopPlatformTrackingAsync(cancellationToken);
                    _isRunning = false;
                }

                return;
            }

            if (_isRunning)
            {
                return;
            }

            var granted = await RequestPlatformPermissionsAsync(requireBackground: true, cancellationToken);
            if (!granted)
            {
                return;
            }

            await StartPlatformTrackingAsync(cancellationToken);
            _isRunning = true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isRunning)
            {
                return;
            }

            await StopPlatformTrackingAsync(cancellationToken);
            _isRunning = false;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public string GetStatusText()
    {
        if (!_settingsService.GetAutoNarrationEnabled())
        {
            return "Tracking dang tat theo cai dat Auto Narration.";
        }

        if (!_isRunning)
        {
            return SupportsBackgroundTracking
                ? "Tracking san sang nhung chua bat do quyen vi tri hoac service nen chua khoi dong."
                : "Tracking dang chay che do foreground tren nen tang hien tai.";
        }

        var mode = SupportsBackgroundTracking ? "foreground + background" : "foreground";
        return _lastAcceptedSample is null
            ? $"Dang khoi dong GPS tracking ({mode})."
            : $"Dang tracking {mode}. Lan cap nhat cuoi: {_lastAcceptedSample.TimestampUtc.LocalDateTime:HH:mm:ss}.";
    }

    private async void OnAutoNarrationSettingsChanged(object? sender, AutoNarrationSettingsChangedEventArgs e)
    {
        try
        {
            if (e.Enabled)
            {
                await EnsureStartedAsync();
            }
            else
            {
                await StopAsync();
            }
        }
        catch
        {
            // Keep settings changes from surfacing background tracking failures.
        }
    }

    internal async Task PublishLocationAsync(LocationTrackingSample sample)
    {
        if (!ShouldAccept(sample))
        {
            return;
        }

        _lastAcceptedSample = sample;
        LocationChanged?.Invoke(this, sample);
        await Task.CompletedTask;
    }

    private bool ShouldAccept(LocationTrackingSample sample)
    {
        if (sample.TimestampUtc < DateTimeOffset.UtcNow.Subtract(MaxSampleAge))
        {
            return false;
        }

        if (sample.AccuracyMeters is > MaxAcceptedAccuracyMeters)
        {
            return false;
        }

        if (_lastAcceptedSample is null)
        {
            return true;
        }

        if (sample.TimestampUtc <= _lastAcceptedSample.TimestampUtc)
        {
            return false;
        }

        var secondsSinceLast = sample.TimestampUtc - _lastAcceptedSample.TimestampUtc;
        var distanceMeters = Location.CalculateDistance(sample.Location, _lastAcceptedSample.Location, DistanceUnits.Kilometers) * 1000d;
        return secondsSinceLast >= MinSampleInterval || distanceMeters >= MinAcceptedMovementMeters;
    }

    private partial Task<bool> RequestPlatformPermissionsAsync(bool requireBackground, CancellationToken cancellationToken);

    private partial Task StartPlatformTrackingAsync(CancellationToken cancellationToken);

    private partial Task StopPlatformTrackingAsync(CancellationToken cancellationToken);

    private partial bool PlatformSupportsBackgroundTracking();
}
