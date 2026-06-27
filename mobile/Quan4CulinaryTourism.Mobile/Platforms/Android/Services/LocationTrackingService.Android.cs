#if ANDROID
using Android.App;
using Android.Content;
using Microsoft.Maui.ApplicationModel;

namespace Quan4CulinaryTourism.Mobile.Services;

public sealed partial class LocationTrackingService
{
    private EventHandler<LocationTrackingSample>? _androidLocationForwarder;

    private partial async Task<bool> RequestPlatformPermissionsAsync(bool requireBackground, CancellationToken cancellationToken)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (status != PermissionStatus.Granted)
        {
            return false;
        }

        if (!requireBackground)
        {
            return true;
        }

        var backgroundStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        if (backgroundStatus != PermissionStatus.Granted)
        {
            backgroundStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
        }

        return backgroundStatus == PermissionStatus.Granted;
    }

    private partial Task StartPlatformTrackingAsync(CancellationToken cancellationToken)
    {
        _androidLocationForwarder ??= (_, sample) => _ = PublishLocationAsync(sample);
        AndroidLocationTrackingForegroundService.LocationChanged -= _androidLocationForwarder;
        AndroidLocationTrackingForegroundService.LocationChanged += _androidLocationForwarder;

        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(AndroidLocationTrackingForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }

        return Task.CompletedTask;
    }

    private partial Task StopPlatformTrackingAsync(CancellationToken cancellationToken)
    {
        if (_androidLocationForwarder is not null)
        {
            AndroidLocationTrackingForegroundService.LocationChanged -= _androidLocationForwarder;
        }

        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(AndroidLocationTrackingForegroundService));
        context.StopService(intent);
        return Task.CompletedTask;
    }

    private partial bool PlatformSupportsBackgroundTracking() => true;
}
#endif
