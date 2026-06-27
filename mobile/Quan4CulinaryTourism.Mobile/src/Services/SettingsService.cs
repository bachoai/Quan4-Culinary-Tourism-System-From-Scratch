using Quan4CulinaryTourism.Mobile.Config;
using Quan4CulinaryTourism.Mobile.Models;

namespace Quan4CulinaryTourism.Mobile.Services;

public class SettingsService
{
    private const string LanguageKey = "selected_language";
    private const string ThemeKey = "theme_mode";
    private const string FirstLaunchKey = "first_launch";
    private const string AnonymousIdKey = "anonymous_id";
    private const string SessionIdKey = "session_id";
    private const string AutoNarrationKey = "auto_narration_enabled";
    private const string NarrationRadiusKey = "auto_narration_radius";

    public event EventHandler<string>? LanguageChanged;
    public event EventHandler<string>? ThemeChanged;
    public event EventHandler<AutoNarrationSettingsChangedEventArgs>? AutoNarrationSettingsChanged;

    public string GetLanguage() => Preferences.Default.Get(LanguageKey, "vi");

    public void SetLanguage(string lang)
    {
        Preferences.Default.Set(LanguageKey, lang);
        Preferences.Default.Set(FirstLaunchKey, false);
        LanguageChanged?.Invoke(this, lang);
    }

    public string GetTheme() => Preferences.Default.Get(ThemeKey, "system");

    public void SetTheme(string theme)
    {
        Preferences.Default.Set(ThemeKey, theme);
        ThemeChanged?.Invoke(this, theme);
    }

    public bool IsFirstLaunch() => Preferences.Default.Get(FirstLaunchKey, true);

    public string GetOrCreateAnonymousId()
    {
        var value = Preferences.Default.Get(AnonymousIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = $"guest-{Guid.NewGuid():N}";
        Preferences.Default.Set(AnonymousIdKey, value);
        return value;
    }

    public string GetOrCreateSessionId()
    {
        var value = Preferences.Default.Get(SessionIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = $"session-{Guid.NewGuid():N}";
        Preferences.Default.Set(SessionIdKey, value);
        return value;
    }

    public bool GetAutoNarrationEnabled() => Preferences.Default.Get(AutoNarrationKey, true);

    public void SetAutoNarrationEnabled(bool enabled)
    {
        Preferences.Default.Set(AutoNarrationKey, enabled);
        RaiseAutoNarrationSettingsChanged();
    }

    public int GetNarrationRadiusMeters() => Preferences.Default.Get(NarrationRadiusKey, 150);

    public void SetNarrationRadiusMeters(int radius)
    {
        Preferences.Default.Set(NarrationRadiusKey, radius);
        RaiseAutoNarrationSettingsChanged();
    }

    public void UpdateAutoNarrationSettings(bool enabled, int radius)
    {
        Preferences.Default.Set(AutoNarrationKey, enabled);
        Preferences.Default.Set(NarrationRadiusKey, radius);
        RaiseAutoNarrationSettingsChanged();
    }

    public string GetApiBaseUrl() => AppConfig.GetApiBaseUrl();

    public void SetApiBaseUrl(string? url) => AppConfig.SetApiBaseUrl(url);

    public IReadOnlyList<LanguageOption> GetLanguages() =>
    [
        new() { Code = "vi", Name = "Tiếng Việt" },
        new() { Code = "en", Name = "English" },
        new() { Code = "zh", Name = "中文" },
        new() { Code = "ja", Name = "日本語" },
        new() { Code = "ko", Name = "한국어" }
    ];

    public IReadOnlyList<ThemeOption> GetThemes() =>
    [
        new() { Key = "system", Name = "System" },
        new() { Key = "light", Name = "Light" },
        new() { Key = "dark", Name = "Dark" }
    ];

    public IReadOnlyList<ApiEndpointOption> GetApiEndpointOptions() =>
    [
        new() { Name = "Windows localhost", Url = AppConfig.WindowsBaseUrl },
        new() { Name = "Android emulator", Url = AppConfig.AndroidEmulatorBaseUrl },
        new() { Name = "LAN device example", Url = AppConfig.LanExampleBaseUrl }
    ];

    private void RaiseAutoNarrationSettingsChanged()
    {
        AutoNarrationSettingsChanged?.Invoke(this, new AutoNarrationSettingsChangedEventArgs(
            GetAutoNarrationEnabled(),
            GetNarrationRadiusMeters()));
    }
}

public sealed class AutoNarrationSettingsChangedEventArgs : EventArgs
{
    public AutoNarrationSettingsChangedEventArgs(bool enabled, int radiusMeters)
    {
        Enabled = enabled;
        RadiusMeters = radiusMeters;
    }

    public bool Enabled { get; }

    public int RadiusMeters { get; }
}
