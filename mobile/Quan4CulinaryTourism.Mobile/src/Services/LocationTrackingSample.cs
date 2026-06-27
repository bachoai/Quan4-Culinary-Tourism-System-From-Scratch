using Microsoft.Maui.Devices.Sensors;

namespace Quan4CulinaryTourism.Mobile.Services;

public sealed class LocationTrackingSample
{
    public required Location Location { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public double? AccuracyMeters { get; init; }
    public bool IsBackground { get; init; }
    public string Source { get; init; } = string.Empty;
}
