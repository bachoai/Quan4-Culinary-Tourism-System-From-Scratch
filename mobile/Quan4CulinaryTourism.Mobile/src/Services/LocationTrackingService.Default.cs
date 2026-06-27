#if !ANDROID && !IOS
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace Quan4CulinaryTourism.Mobile.Services;

public sealed partial class LocationTrackingService
{
    private CancellationTokenSource? _fallbackTrackingCts;
    private Task? _fallbackTrackingTask;

    private partial async Task<bool> RequestPlatformPermissionsAsync(bool requireBackground, CancellationToken cancellationToken)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        return status == PermissionStatus.Granted;
    }

    private partial Task StartPlatformTrackingAsync(CancellationToken cancellationToken)
    {
        _fallbackTrackingCts?.Cancel();
        _fallbackTrackingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _fallbackTrackingTask = RunFallbackTrackingAsync(_fallbackTrackingCts.Token);
        return Task.CompletedTask;
    }

    private partial Task StopPlatformTrackingAsync(CancellationToken cancellationToken)
    {
        _fallbackTrackingCts?.Cancel();
        _fallbackTrackingCts = null;
        _fallbackTrackingTask = null;
        return Task.CompletedTask;
    }

    private partial bool PlatformSupportsBackgroundTracking() => false;

    private async Task RunFallbackTrackingAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(12));
        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var location = await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(12)),
                    cancellationToken);

                if (location is null)
                {
                    continue;
                }

                await PublishLocationAsync(new LocationTrackingSample
                {
                    Location = location,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    AccuracyMeters = location.Accuracy,
                    IsBackground = false,
                    Source = "fallback"
                });
            }
            catch
            {
                // Ignore transient location failures on unsupported platforms.
            }
        }
    }
}
#endif
