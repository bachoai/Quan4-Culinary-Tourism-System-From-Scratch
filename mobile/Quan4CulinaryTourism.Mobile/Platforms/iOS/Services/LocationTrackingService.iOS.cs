#if IOS
using CoreLocation;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using UIKit;

namespace Quan4CulinaryTourism.Mobile.Services;

public sealed partial class LocationTrackingService
{
    private CLLocationManager? _iosLocationManager;
    private IosLocationTrackingDelegate? _iosDelegate;
    private TaskCompletionSource<bool>? _iosPermissionTcs;

    private partial async Task<bool> RequestPlatformPermissionsAsync(bool requireBackground, CancellationToken cancellationToken)
    {
        EnsureIosManager();

        var currentStatus = CLLocationManager.Status;
        if (!requireBackground && currentStatus == CLAuthorizationStatus.AuthorizedWhenInUse)
        {
            return true;
        }

        if (currentStatus == CLAuthorizationStatus.AuthorizedAlways)
        {
            return true;
        }

        _iosPermissionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (requireBackground)
            {
                _iosLocationManager!.RequestAlwaysAuthorization();
            }
            else
            {
                _iosLocationManager!.RequestWhenInUseAuthorization();
            }
        });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        using var registration = timeoutCts.Token.Register(() => _iosPermissionTcs.TrySetResult(false));
        return await _iosPermissionTcs.Task;
    }

    private partial Task StartPlatformTrackingAsync(CancellationToken cancellationToken)
    {
        EnsureIosManager();
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            _iosLocationManager!.StartUpdatingLocation();
            _iosLocationManager.StartMonitoringSignificantLocationChanges();
        });
    }

    private partial Task StopPlatformTrackingAsync(CancellationToken cancellationToken)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            _iosLocationManager?.StopUpdatingLocation();
            _iosLocationManager?.StopMonitoringSignificantLocationChanges();
        });
    }

    private partial bool PlatformSupportsBackgroundTracking() => true;

    private void EnsureIosManager()
    {
        if (_iosLocationManager is not null)
        {
            return;
        }

        _iosDelegate = new IosLocationTrackingDelegate(this);
        _iosLocationManager = new CLLocationManager
        {
            DesiredAccuracy = CLLocation.AccuracyNearestTenMeters,
            DistanceFilter = 20,
            AllowsBackgroundLocationUpdates = true,
            PausesLocationUpdatesAutomatically = true
        };
        _iosLocationManager.Delegate = _iosDelegate;
    }

    private void UpdateIosPermissionStatus(CLAuthorizationStatus status)
    {
        var granted = status == CLAuthorizationStatus.AuthorizedAlways || status == CLAuthorizationStatus.AuthorizedWhenInUse;
        _iosPermissionTcs?.TrySetResult(granted);
    }

    private static DateTimeOffset ToDateTimeOffset(NSDate timestamp)
    {
        var milliseconds = (long)Math.Round(timestamp.SecondsSince1970 * 1000d);
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }

    private sealed class IosLocationTrackingDelegate : CLLocationManagerDelegate
    {
        private readonly LocationTrackingService _owner;

        public IosLocationTrackingDelegate(LocationTrackingService owner)
        {
            _owner = owner;
        }

        public override void AuthorizationChanged(CLLocationManager manager, CLAuthorizationStatus status)
        {
            _owner.UpdateIosPermissionStatus(status);
        }

        public override void DidChangeAuthorization(CLLocationManager manager)
        {
            _owner.UpdateIosPermissionStatus(manager.AuthorizationStatus);
        }

        public override void LocationsUpdated(CLLocationManager manager, CLLocation[] locations)
        {
            var latest = locations.LastOrDefault();
            if (latest is null)
            {
                return;
            }

            var sample = new LocationTrackingSample
            {
                Location = new Location(latest.Coordinate.Latitude, latest.Coordinate.Longitude)
                {
                    Accuracy = latest.HorizontalAccuracy >= 0 ? latest.HorizontalAccuracy : null,
                    Altitude = latest.VerticalAccuracy >= 0 ? latest.Altitude : null,
                    Speed = latest.Speed >= 0 ? latest.Speed : null,
                    Course = latest.Course >= 0 ? latest.Course : null,
                    Timestamp = ToDateTimeOffset(latest.Timestamp)
                },
                TimestampUtc = ToDateTimeOffset(latest.Timestamp),
                AccuracyMeters = latest.HorizontalAccuracy >= 0 ? latest.HorizontalAccuracy : null,
                IsBackground = UIApplication.SharedApplication.ApplicationState != UIApplicationState.Active,
                Source = "ios-core-location"
            };

            _ = _owner.PublishLocationAsync(sample);
        }

        public override void Failed(CLLocationManager manager, NSError error)
        {
            _owner._iosPermissionTcs?.TrySetResult(false);
        }
    }
}
#endif
