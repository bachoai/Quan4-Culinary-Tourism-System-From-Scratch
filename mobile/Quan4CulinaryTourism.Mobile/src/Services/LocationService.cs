using Microsoft.Maui.Devices.Sensors;

namespace Quan4CulinaryTourism.Mobile.Services;

public class LocationService
{
    private readonly LocationTrackingService _locationTrackingService;

    public LocationService(LocationTrackingService locationTrackingService)
    {
        _locationTrackingService = locationTrackingService;
    }

    public async Task<bool> CheckAndRequestPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return status == PermissionStatus.Granted;
    }

    public async Task<Location?> GetCurrentLocationAsync()
    {
        var cachedLocation = _locationTrackingService.LastKnownLocation;
        var cachedAt = _locationTrackingService.LastKnownTimestampUtc;
        if (cachedLocation is not null && cachedAt is not null && DateTimeOffset.UtcNow - cachedAt <= TimeSpan.FromMinutes(2))
        {
            return cachedLocation;
        }

        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            return await Geolocation.Default.GetLocationAsync(request);
        }
        catch (FeatureNotEnabledException)
        {
            return null;
        }
        catch (PermissionException)
        {
            return null;
        }
    }

    public async Task<bool> IsLocationEnabledAsync()
    {
        try
        {
            await Geolocation.Default.GetLastKnownLocationAsync();
            return true;
        }
        catch (FeatureNotEnabledException)
        {
            return false;
        }
    }
}
