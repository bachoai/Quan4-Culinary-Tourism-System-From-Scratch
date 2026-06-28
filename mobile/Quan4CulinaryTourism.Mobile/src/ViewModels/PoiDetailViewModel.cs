using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quan4CulinaryTourism.Mobile.DTOs;
using Quan4CulinaryTourism.Mobile.Models;
using Quan4CulinaryTourism.Mobile.Services;

namespace Quan4CulinaryTourism.Mobile.ViewModels;

public partial class PoiDetailViewModel : BaseViewModel
{
    private readonly PoiApiService _poiApiService;
    private readonly AudioApiService _audioApiService;
    private readonly OfflineDatabaseService _offlineDatabaseService;
    private readonly ConnectivityService _connectivityService;
    private readonly AudioPlayerService _audioPlayerService;
    private readonly AudioDownloadService _audioDownloadService;
    private readonly AnalyticsApiService _analyticsApiService;
    private readonly SettingsService _settingsService;
    private bool _audioEventsAttached;

    [ObservableProperty]
    private PoiDetailResponse? poi;

    [ObservableProperty]
    private PoiAudioResponse? audio;

    [ObservableProperty]
    private string audioState = "Idle";

    [ObservableProperty]
    private string audioMessage = "Dia diem nay chua co noi dung thuyet minh.";

    [ObservableProperty]
    private string selectedLanguage = "vi";

    [ObservableProperty]
    private string offlineAudioState = "NotDownloaded";

    [ObservableProperty]
    private bool hasOfflineAudio;

    public PoiDetailViewModel(
        PoiApiService poiApiService,
        AudioApiService audioApiService,
        OfflineDatabaseService offlineDatabaseService,
        ConnectivityService connectivityService,
        AudioPlayerService audioPlayerService,
        AudioDownloadService audioDownloadService,
        AnalyticsApiService analyticsApiService,
        SettingsService settingsService)
    {
        _poiApiService = poiApiService;
        _audioApiService = audioApiService;
        _offlineDatabaseService = offlineDatabaseService;
        _connectivityService = connectivityService;
        _audioPlayerService = audioPlayerService;
        _audioDownloadService = audioDownloadService;
        _analyticsApiService = analyticsApiService;
        _settingsService = settingsService;

        Title = "Chi tiet dia diem";
    }

    public async Task InitializeAsync(string poiId, string? autoplayMode = null, string source = "detail")
    {
        SelectedLanguage = _settingsService.GetLanguage();
        await LoadAsync(poiId, autoplayMode, source);
    }

    public void AttachAudioEvents()
    {
        if (_audioEventsAttached)
        {
            return;
        }

        _audioPlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
        _audioEventsAttached = true;
    }

    public void DetachAudioEvents()
    {
        if (!_audioEventsAttached)
        {
            return;
        }

        _audioPlayerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioEventsAttached = false;
    }

    [RelayCommand]
    private async Task PlayAudioAsync()
    {
        if (Poi is not null && !string.IsNullOrWhiteSpace(Poi.NarrationText))
        {
            try
            {
                await _audioPlayerService.SpeakPoiDescriptionAsync(
                    Poi.Id,
                    SelectedLanguage,
                    Poi.NarrationText,
                    Poi.Name,
                    "detail");
            }
            catch (Exception ex)
            {
                AudioState = "Error";
                AudioMessage = ex.Message;
            }

            return;
        }

        if (Audio is null)
        {
            AudioState = "Error";
            AudioMessage = "Khong co noi dung thuyet minh de phat.";
            return;
        }

        try
        {
            await _audioPlayerService.PlayPoiAudioAsync(
                Poi?.Id ?? "manual-audio",
                SelectedLanguage,
                Audio.AudioUrl,
                Audio.LocalAudioPath,
                Poi?.Name,
                "detail");
        }
        catch (Exception ex)
        {
            AudioState = "Error";
            AudioMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PauseAudioAsync()
    {
        await _audioPlayerService.PauseAsync();
        AudioState = "Paused";
        AudioMessage = "Thuyet minh da tam dung.";
    }

    [RelayCommand]
    private async Task StopAudioAsync()
    {
        await _audioPlayerService.StopAsync();
        AudioState = "Idle";
        AudioMessage = "Da dung thuyet minh.";
    }

    [RelayCommand]
    private async Task DownloadOfflineAudioAsync()
    {
        if (Audio is null)
        {
            OfflineAudioState = "Failed";
            AudioMessage = "Chua co audio de tai offline.";
            return;
        }

        try
        {
            OfflineAudioState = "Downloading";
            AudioMessage = "Dang tai audio offline...";
            Audio.LocalAudioPath = await _audioDownloadService.DownloadAsync(Audio);
            HasOfflineAudio = File.Exists(Audio.LocalAudioPath);
            OfflineAudioState = HasOfflineAudio ? "Downloaded" : "Failed";
            AudioMessage = HasOfflineAudio ? "Da tai audio offline." : "Khong luu duoc audio offline.";

            await _analyticsApiService.CollectAsync(new CollectAnalyticsRequest
            {
                EventName = "offline_audio_downloaded",
                PoiId = Poi?.Id,
                Lang = SelectedLanguage
            });
        }
        catch (Exception ex)
        {
            OfflineAudioState = "Failed";
            AudioMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SpeakWithTtsAsync()
    {
        if (Poi is null || string.IsNullOrWhiteSpace(Poi.NarrationText))
        {
            AudioMessage = "Khong co mo ta de doc bang TTS.";
            return;
        }

        try
        {
            await _audioPlayerService.SpeakPoiDescriptionAsync(
                Poi.Id,
                SelectedLanguage,
                Poi.NarrationText,
                Poi.Name,
                "detail");
        }
        catch
        {
            AudioState = "Error";
            AudioMessage = "Thiet bi khong phat duoc TTS o thoi diem nay.";
        }
    }

    [RelayCommand]
    private async Task OpenDirectionsAsync()
    {
        if (Poi is null)
        {
            return;
        }

        await Launcher.Default.OpenAsync(Poi.ResolvedMapUrl);
    }

    [RelayCommand]
    private async Task OpenOnMapAsync()
    {
        if (Poi is null)
        {
            return;
        }

        await Shell.Current.GoToAsync("//map");
    }

    private async Task LoadAsync(string poiId, string? autoplayMode, string source)
    {
        await RunBusyAsync(async () =>
        {
            await _offlineDatabaseService.InitializeAsync();
            if (_connectivityService.IsOnline())
            {
                Poi = await _poiApiService.GetByIdAsync(poiId, SelectedLanguage);
                Audio = await _audioApiService.GetPoiAudioAsync(poiId, SelectedLanguage);

                if (Poi is not null)
                {
                    await _offlineDatabaseService.SavePoiDetailAsync(Poi);
                }

                if (Audio is not null)
                {
                    await _offlineDatabaseService.SavePoiAudioAsync(Audio);
                }
            }
            else
            {
                Poi = await _offlineDatabaseService.GetPoiDetailAsync(poiId);
                Audio = await _offlineDatabaseService.GetPoiAudioAsync(poiId, SelectedLanguage);
            }

            if (Poi is null)
            {
                SetError("Khong tim thay du lieu chi tiet cho dia diem nay.");
                return;
            }

            Title = Poi.Name;
            if (string.IsNullOrWhiteSpace(Poi.CategoryName))
            {
                var categories = await _offlineDatabaseService.GetCategoriesAsync();
                Poi.CategoryName = categories.FirstOrDefault(item => item.Id == Poi.CategoryId)?.Name ?? "Khac";
            }

            HasOfflineAudio = !string.IsNullOrWhiteSpace(Audio?.LocalAudioPath) && File.Exists(Audio.LocalAudioPath);
            OfflineAudioState = HasOfflineAudio ? "Downloaded" : "NotDownloaded";

            await _analyticsApiService.CollectAsync(new CollectAnalyticsRequest
            {
                EventName = "poi_viewed",
                PoiId = Poi.Id,
                Lang = SelectedLanguage,
                Metadata =
                {
                    ["source"] = source
                }
            });

            if (!string.IsNullOrWhiteSpace(autoplayMode))
            {
                await TryAutoplayAsync(autoplayMode, source);
            }
        }, "Khong tai duoc chi tiet dia diem.");
    }

    private async Task TryAutoplayAsync(string autoplayMode, string source)
    {
        if (Poi is null)
        {
            return;
        }

        var normalizedMode = autoplayMode.Trim().ToLowerInvariant();
        var hasAudio = Audio is not null && (!string.IsNullOrWhiteSpace(Audio.AudioUrl) || !string.IsNullOrWhiteSpace(Audio.LocalAudioPath));
        var hasTts = !string.IsNullOrWhiteSpace(Poi.NarrationText);

        if (hasTts)
        {
            await _audioPlayerService.SpeakPoiDescriptionAsync(Poi.Id, SelectedLanguage, Poi.NarrationText, Poi.Name, source);
            return;
        }

        if ((normalizedMode == "audio" || normalizedMode == "prefer_audio" || normalizedMode == "tts") && hasAudio)
        {
            await _audioPlayerService.PlayPoiAudioAsync(Poi.Id, SelectedLanguage, Audio!.AudioUrl, Audio.LocalAudioPath, Poi.Name, source);
        }
    }

    private void OnPlaybackStateChanged(object? sender, AudioPlaybackStateChangedEventArgs args)
    {
        if (Poi?.Id is null || args.PoiId != Poi.Id)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AudioState = args.State.ToString();
            AudioMessage = MapAudioMessage(args);
        });
    }

    private static string MapAudioMessage(AudioPlaybackStateChangedEventArgs args)
    {
        return args.State switch
        {
            AudioPlaybackState.Queued => "Da them vao hang cho thuyet minh.",
            AudioPlaybackState.Preparing => args.ContentType == AudioPlaybackContentType.TextToSpeech
                ? "Dang chuan bi TTS..."
                : "Dang chuan bi audio...",
            AudioPlaybackState.Playing => args.ContentType == AudioPlaybackContentType.TextToSpeech
                ? "Dang doc mo ta bang TTS."
                : args.Message,
            AudioPlaybackState.Paused => "Thuyet minh da tam dung.",
            AudioPlaybackState.Stopped => "Da dung thuyet minh.",
            AudioPlaybackState.Completed => "Da phat xong thuyet minh.",
            AudioPlaybackState.Interrupted => "Thuyet minh bi dung vi co nguon am thanh khac chen vao.",
            AudioPlaybackState.Error => args.Message,
            _ => "San sang phat thuyet minh."
        };
    }
}
