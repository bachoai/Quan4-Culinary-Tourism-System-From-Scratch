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
    private string audioMessage = "Địa điểm này chưa có audio thuyết minh.";

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

        Title = "Chi tiết địa điểm";
    }

    public async Task InitializeAsync(string poiId)
    {
        SelectedLanguage = _settingsService.GetLanguage();
        await LoadAsync(poiId);
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
        if (Audio is null)
        {
            AudioState = "Error";
            AudioMessage = "Địa điểm này chưa có audio thuyết minh.";
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

            await _analyticsApiService.CollectAsync(new CollectAnalyticsRequest
            {
                EventName = "audio_played",
                PoiId = Poi?.Id,
                Lang = SelectedLanguage
            });
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
        AudioMessage = "Audio đã tạm dừng.";
    }

    [RelayCommand]
    private async Task StopAudioAsync()
    {
        await _audioPlayerService.StopAsync();
        AudioState = "Idle";
        AudioMessage = "Đã dừng audio.";
    }

    [RelayCommand]
    private async Task DownloadOfflineAudioAsync()
    {
        if (Audio is null)
        {
            OfflineAudioState = "Failed";
            AudioMessage = "Chưa có audio để tải offline.";
            return;
        }

        try
        {
            OfflineAudioState = "Downloading";
            AudioMessage = "Đang tải audio offline...";
            Audio.LocalAudioPath = await _audioDownloadService.DownloadAsync(Audio);
            HasOfflineAudio = File.Exists(Audio.LocalAudioPath);
            OfflineAudioState = HasOfflineAudio ? "Downloaded" : "Failed";
            AudioMessage = HasOfflineAudio ? "Đã tải audio offline." : "Không lưu được audio offline.";

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
            AudioMessage = "Không có mô tả để đọc bằng TTS.";
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

            await _analyticsApiService.CollectAsync(new CollectAnalyticsRequest
            {
                EventName = "tts_played",
                PoiId = Poi.Id,
                Lang = SelectedLanguage
            });
        }
        catch
        {
            AudioState = "Error";
            AudioMessage = "Thiết bị không phát được TTS ở thời điểm này.";
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

    private async Task LoadAsync(string poiId)
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
                SetError("Không tìm thấy dữ liệu chi tiết cho địa điểm này.");
                return;
            }

            Title = Poi.Name;
            if (string.IsNullOrWhiteSpace(Poi.CategoryName))
            {
                var categories = await _offlineDatabaseService.GetCategoriesAsync();
                Poi.CategoryName = categories.FirstOrDefault(item => item.Id == Poi.CategoryId)?.Name ?? "Khác";
            }

            HasOfflineAudio = !string.IsNullOrWhiteSpace(Audio?.LocalAudioPath) && File.Exists(Audio.LocalAudioPath);
            OfflineAudioState = HasOfflineAudio ? "Downloaded" : "NotDownloaded";

            await _analyticsApiService.CollectAsync(new CollectAnalyticsRequest
            {
                EventName = "poi_viewed",
                PoiId = Poi.Id,
                Lang = SelectedLanguage
            });
        }, "Không tải được chi tiết địa điểm.");
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
            AudioPlaybackState.Queued => "Đã thêm vào hàng chờ thuyết minh.",
            AudioPlaybackState.Preparing => args.ContentType == AudioPlaybackContentType.TextToSpeech
                ? "Đang chuẩn bị TTS..."
                : "Đang chuẩn bị audio...",
            AudioPlaybackState.Playing => args.ContentType == AudioPlaybackContentType.TextToSpeech
                ? "Đang đọc mô tả bằng TTS."
                : args.Message,
            AudioPlaybackState.Paused => "Audio đã tạm dừng.",
            AudioPlaybackState.Stopped => "Đã dừng audio.",
            AudioPlaybackState.Completed => "Đã phát xong thuyết minh.",
            AudioPlaybackState.Interrupted => "Audio bị dừng vì có nguồn âm thanh khác hoặc thông báo chen vào.",
            AudioPlaybackState.Error => args.Message,
            _ => "Sẵn sàng phát thuyết minh."
        };
    }
}
