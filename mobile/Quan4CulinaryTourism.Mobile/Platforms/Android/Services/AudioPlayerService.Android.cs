using Android.App;
using Android.Content;
using Android.Media;

namespace Quan4CulinaryTourism.Mobile.Services;

public partial class AudioPlayerService
{
    private AudioManager? _androidAudioManager;
    private AudioFocusRequestClass? _androidAudioFocusRequest;
    private AudioFocusChangeListener? _androidAudioFocusChangeListener;

    partial void ConfigurePlatformAudioHooks()
    {
        _androidAudioManager = Android.App.Application.Context.GetSystemService(Context.AudioService) as AudioManager;
        _androidAudioFocusChangeListener = new AudioFocusChangeListener(this);
        _requestPlatformPlaybackAccessAsync = RequestPlatformPlaybackAccessAsync;
        _releasePlatformPlaybackAccess = ReleasePlatformPlaybackAccess;
    }

    private Task RequestPlatformPlaybackAccessAsync(CancellationToken cancellationToken)
    {
        if (_androidAudioManager is null || _androidAudioFocusChangeListener is null)
        {
            return Task.CompletedTask;
        }

        var focusState = AudioFocusRequest.Granted;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var audioAttributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)
                .SetContentType(AudioContentType.Speech)
                .Build()
                ?? throw new InvalidOperationException("Khong tao duoc audio attributes tren Android.");

            _androidAudioFocusRequest ??= new AudioFocusRequestClass.Builder(AudioFocus.GainTransientMayDuck)
                .SetAudioAttributes(audioAttributes)
                .SetAcceptsDelayedFocusGain(false)
                .SetOnAudioFocusChangeListener(_androidAudioFocusChangeListener)
                .Build()
                ?? throw new InvalidOperationException("Khong tao duoc audio focus request tren Android.");

            focusState = _androidAudioManager.RequestAudioFocus(_androidAudioFocusRequest);
        }
        else
        {
#pragma warning disable CA1422
            focusState = (AudioFocusRequest)(int)_androidAudioManager.RequestAudioFocus(
                _androidAudioFocusChangeListener,
                Android.Media.Stream.Music,
                AudioFocus.GainTransientMayDuck);
#pragma warning restore CA1422
        }

        if (focusState != AudioFocusRequest.Granted)
        {
            throw new InvalidOperationException("Khong lay duoc audio focus de phat thuyet minh.");
        }

        return Task.CompletedTask;
    }

    private void ReleasePlatformPlaybackAccess()
    {
        if (_androidAudioManager is null || _androidAudioFocusChangeListener is null)
        {
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(26) && _androidAudioFocusRequest is not null)
        {
            _androidAudioManager.AbandonAudioFocusRequest(_androidAudioFocusRequest);
            return;
        }

#pragma warning disable CA1422
        _androidAudioManager.AbandonAudioFocus(_androidAudioFocusChangeListener);
#pragma warning restore CA1422
    }

    private sealed class AudioFocusChangeListener(AudioPlayerService service)
        : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            if (focusChange is AudioFocus.Loss or AudioFocus.LossTransient or AudioFocus.LossTransientCanDuck)
            {
                service.NotifyPlatformInterruption("android_audio_focus_lost");
            }
        }
    }
}
