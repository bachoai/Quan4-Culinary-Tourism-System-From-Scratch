using AVFoundation;
using Foundation;

namespace Quan4CulinaryTourism.Mobile.Services;

public partial class AudioPlayerService
{
    private NSObject? _iosInterruptionObserver;

    partial void ConfigurePlatformAudioHooks()
    {
        _requestPlatformPlaybackAccessAsync = RequestPlatformPlaybackAccessAsync;
        _releasePlatformPlaybackAccess = ReleasePlatformPlaybackAccess;
        _disposePlatformAudioSession = DisposePlatformAudioSession;
        _iosInterruptionObserver = AVAudioSession.Notifications.ObserveInterruption((_, args) =>
        {
            if (args.InterruptionType == AVAudioSessionInterruptionType.Began)
            {
                NotifyPlatformInterruption("ios_audio_interrupted");
            }
        });
    }

    private Task RequestPlatformPlaybackAccessAsync(CancellationToken cancellationToken)
    {
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.InterruptSpokenAudioAndMixWithOthers);
        session.SetActive(true);
        return Task.CompletedTask;
    }

    private void ReleasePlatformPlaybackAccess()
    {
        AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
    }

    private void DisposePlatformAudioSession()
    {
        _iosInterruptionObserver?.Dispose();
        _iosInterruptionObserver = null;
    }
}
