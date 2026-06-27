using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using Quan4CulinaryTourism.Mobile.LocalDatabase;
using Quan4CulinaryTourism.Mobile.Services;
using Quan4CulinaryTourism.Mobile.ViewModels;
using Quan4CulinaryTourism.Mobile.Views;

namespace Quan4CulinaryTourism.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiMaps()
            .AddAudio()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<ConnectivityService>();
        builder.Services.AddSingleton<LocalDbContext>();
        builder.Services.AddSingleton<OfflineDatabaseService>();
        builder.Services.AddSingleton<LocationTrackingService>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<AudioPlayerService>();
        builder.Services.AddSingleton<AudioDownloadService>();
        builder.Services.AddSingleton<HealthApiService>();
        builder.Services.AddSingleton<CategoryApiService>();
        builder.Services.AddSingleton<PoiApiService>();
        builder.Services.AddSingleton<AudioApiService>();
        builder.Services.AddSingleton<AnalyticsApiService>();
        builder.Services.AddSingleton<MapsApiService>();
        builder.Services.AddSingleton<GeofenceService>();
        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddTransient<SplashViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<PoiListViewModel>();
        builder.Services.AddTransient<PoiDetailViewModel>();
        builder.Services.AddTransient<NearbyViewModel>();
        builder.Services.AddTransient<MapViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<QrEntryViewModel>();

        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<PoiListPage>();
        builder.Services.AddTransient<PoiDetailPage>();
        builder.Services.AddTransient<NearbyPage>();
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<QrEntryPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
