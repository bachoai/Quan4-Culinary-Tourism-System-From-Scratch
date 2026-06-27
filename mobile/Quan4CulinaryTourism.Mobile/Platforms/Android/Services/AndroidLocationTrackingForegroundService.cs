#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Microsoft.Maui.Devices;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace Quan4CulinaryTourism.Mobile.Services;

[Service(Enabled = true, Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class AndroidLocationTrackingForegroundService : Service, ILocationListener
{
    private const string ChannelId = "quan4-location-tracking";
    private const int NotificationId = 4201;
    private const long MinUpdateTimeMs = 10000;
    private const float MinUpdateDistanceMeters = 20f;

    private LocationManager? _locationManager;

    public static event EventHandler<LocationTrackingSample>? LocationChanged;

    public override void OnCreate()
    {
        base.OnCreate();
        _locationManager = GetSystemService(LocationService) as LocationManager;
        EnsureNotificationChannel();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, BuildNotification());
        RegisterLocationUpdates();
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        try
        {
            _locationManager?.RemoveUpdates(this);
        }
        catch
        {
            // Ignore teardown failures from provider state changes.
        }
    }

    public void OnLocationChanged(Android.Locations.Location location)
    {
        LocationChanged?.Invoke(this, new LocationTrackingSample
        {
            Location = new MauiLocation(location.Latitude, location.Longitude)
            {
                Accuracy = location.HasAccuracy ? location.Accuracy : null,
                Altitude = location.HasAltitude ? location.Altitude : null,
                Speed = location.HasSpeed ? location.Speed : null,
                Course = location.HasBearing ? location.Bearing : null,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(location.Time)
            },
            TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(location.Time),
            AccuracyMeters = location.HasAccuracy ? location.Accuracy : null,
            IsBackground = true,
            Source = location.Provider ?? "android-location"
        });
    }

    public void OnProviderDisabled(string provider)
    {
    }

    public void OnProviderEnabled(string provider)
    {
    }

#pragma warning disable CS8767
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras)
    {
    }
#pragma warning restore CS8767

    private void RegisterLocationUpdates()
    {
        if (_locationManager is null)
        {
            return;
        }

        try
        {
            if (_locationManager.IsProviderEnabled(LocationManager.GpsProvider))
            {
                _locationManager.RequestLocationUpdates(LocationManager.GpsProvider, MinUpdateTimeMs, MinUpdateDistanceMeters, this, Android.OS.Looper.MainLooper);
                PublishLastKnownLocation(LocationManager.GpsProvider);
            }

            if (_locationManager.IsProviderEnabled(LocationManager.NetworkProvider))
            {
                _locationManager.RequestLocationUpdates(LocationManager.NetworkProvider, MinUpdateTimeMs, MinUpdateDistanceMeters, this, Android.OS.Looper.MainLooper);
                PublishLastKnownLocation(LocationManager.NetworkProvider);
            }
        }
        catch
        {
            // Permission or provider errors are handled by the shared tracking service.
        }
    }

    private void PublishLastKnownLocation(string provider)
    {
        try
        {
            var location = _locationManager?.GetLastKnownLocation(provider);
            if (location is not null)
            {
                OnLocationChanged(location);
            }
        }
        catch
        {
            // Ignore provider cache failures.
        }
    }

    private Notification BuildNotification()
    {
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            flags |= PendingIntentFlags.Immutable;
        }

        var pendingIntent = launchIntent is null
            ? null
            : PendingIntent.GetActivity(this, 0, launchIntent, flags);

        var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);

        builder
            .SetContentTitle("Quan4 GPS Tracking")
            .SetContentText("Dang theo doi vi tri de kich hoat audio theo geofence.")
            .SetOngoing(true)
            .SetSmallIcon(ApplicationInfo?.Icon ?? Resource.Mipmap.appicon);

        if (pendingIntent is not null)
        {
            builder.SetContentIntent(pendingIntent);
        }

        return builder.Build();
    }

    private void EnsureNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = GetSystemService(NotificationService) as NotificationManager;
        if (manager?.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, "Location Tracking", NotificationImportance.Low)
        {
            Description = "Nen cho geofence va auto narration."
        };
        manager?.CreateNotificationChannel(channel);
    }
}
#endif
