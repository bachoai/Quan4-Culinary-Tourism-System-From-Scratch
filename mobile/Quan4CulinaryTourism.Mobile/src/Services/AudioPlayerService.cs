using Plugin.Maui.Audio;
using Quan4CulinaryTourism.Mobile.Config;

namespace Quan4CulinaryTourism.Mobile.Services;

public partial class AudioPlayerService : IDisposable
{
    private readonly IAudioManager _audioManager;
    private readonly HttpClient _httpClient;
    private readonly Lock _syncRoot = new();
    private readonly LinkedList<AudioPlaybackRequest> _queue = [];
    private readonly HashSet<string> _pendingKeys = [];
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly Task _queueWorker;
    private readonly Func<CancellationToken, Task> _noopRequestAccess = static _ => Task.CompletedTask;
    private readonly Action _noopAction = static () => { };
    private Func<CancellationToken, Task> _requestPlatformPlaybackAccessAsync;
    private Action _releasePlatformPlaybackAccess;
    private Action _disposePlatformAudioSession;
    private IAudioPlayer? _player;
    private MemoryStream? _currentBuffer;
    private Stream? _currentStream;
    private CancellationTokenSource? _currentPlaybackCts;
    private AudioPlaybackRequest? _currentRequest;
    private AudioPlaybackState _currentState = AudioPlaybackState.Idle;
    private bool _disposed;

    public AudioPlayerService(IAudioManager audioManager, HttpClient httpClient)
    {
        _audioManager = audioManager;
        _httpClient = httpClient;
        _requestPlatformPlaybackAccessAsync = _noopRequestAccess;
        _releasePlatformPlaybackAccess = _noopAction;
        _disposePlatformAudioSession = _noopAction;
        ConfigurePlatformAudioHooks();
        _queueWorker = Task.Run(ProcessQueueAsync);
    }

    public event EventHandler<AudioPlaybackStateChangedEventArgs>? PlaybackStateChanged;

    public bool IsPlaying
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentState is AudioPlaybackState.Preparing or AudioPlaybackState.Playing or AudioPlaybackState.Paused;
            }
        }
    }

    public Task PlayAsync(string? audioUrl, string? localAudioPath = null)
    {
        return PlayPoiAudioAsync(
            "manual-audio",
            "vi",
            audioUrl,
            localAudioPath,
            "Audio thu cong",
            "detail");
    }

    public async Task PlayPoiAudioAsync(
        string poiId,
        string language,
        string? audioUrl,
        string? localAudioPath = null,
        string? title = null,
        string source = "detail")
    {
        if (string.IsNullOrWhiteSpace(localAudioPath) && string.IsNullOrWhiteSpace(audioUrl))
        {
            throw new InvalidOperationException("Dia diem nay chua co audio thuyet minh.");
        }

        var request = AudioPlaybackRequest.CreateAudio(
            poiId,
            language,
            source,
            title,
            audioUrl,
            localAudioPath);

        if (await TryHandleDuplicateImmediateAsync(request))
        {
            return;
        }

        await StopAsync();
        EnqueueRequest(request, prioritize: true);
    }

    public async Task SpeakPoiDescriptionAsync(
        string poiId,
        string language,
        string text,
        string? title = null,
        string source = "detail")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Khong co mo ta de doc bang TTS.");
        }

        var request = AudioPlaybackRequest.CreateTts(
            poiId,
            language,
            source,
            title,
            text);

        if (await TryHandleDuplicateImmediateAsync(request))
        {
            return;
        }

        await StopAsync();
        EnqueueRequest(request, prioritize: true);
    }

    public Task<bool> QueuePoiAudioAsync(
        string poiId,
        string language,
        string? audioUrl,
        string? localAudioPath = null,
        string? title = null,
        string source = "geofence")
    {
        if (string.IsNullOrWhiteSpace(localAudioPath) && string.IsNullOrWhiteSpace(audioUrl))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(EnqueueRequest(
            AudioPlaybackRequest.CreateAudio(
                poiId,
                language,
                source,
                title,
                audioUrl,
                localAudioPath)));
    }

    public Task<bool> QueuePoiTtsAsync(
        string poiId,
        string language,
        string text,
        string? title = null,
        string source = "geofence")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(EnqueueRequest(
            AudioPlaybackRequest.CreateTts(
                poiId,
                language,
                source,
                title,
                text)));
    }

    public Task PauseAsync()
    {
        AudioPlaybackRequest? request;
        lock (_syncRoot)
        {
            if (_currentRequest is null || _player is null || !_player.IsPlaying)
            {
                return Task.CompletedTask;
            }

            _player.Pause();
            _currentState = AudioPlaybackState.Paused;
            request = _currentRequest;
        }

        RaiseStateChanged(AudioPlaybackState.Paused, request, "Audio da tam dung.");
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        AudioPlaybackRequest? request;
        lock (_syncRoot)
        {
            if (_currentRequest is null || _player is null)
            {
                return Task.CompletedTask;
            }

            _player.Play();
            _currentState = AudioPlaybackState.Playing;
            request = _currentRequest;
        }

        RaiseStateChanged(AudioPlaybackState.Playing, request, BuildPlayingMessage(request));
        return Task.CompletedTask;
    }

    public Task StopAsync(bool clearQueue = true)
    {
        return StopInternalAsync(clearQueue, AudioPlaybackState.Stopped, "Da dung audio.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queueSignal.Release();
        _currentPlaybackCts?.Cancel();
        ReleasePlaybackResources();
        _releasePlatformPlaybackAccess();
        _disposePlatformAudioSession();
        _queueSignal.Dispose();
    }

    partial void ConfigurePlatformAudioHooks();

    private bool EnqueueRequest(AudioPlaybackRequest request, bool prioritize = false)
    {
        lock (_syncRoot)
        {
            if (IsDuplicateLocked(request))
            {
                return false;
            }

            if (prioritize)
            {
                _queue.AddFirst(request);
            }
            else
            {
                _queue.AddLast(request);
            }

            _pendingKeys.Add(request.Key);
        }

        RaiseStateChanged(AudioPlaybackState.Queued, request, "Da them vao hang cho thuyet minh.");
        _queueSignal.Release();
        return true;
    }

    private bool IsDuplicateLocked(AudioPlaybackRequest request)
    {
        return (_currentRequest is not null && _currentRequest.Key == request.Key)
            || _pendingKeys.Contains(request.Key);
    }

    private async Task<bool> TryHandleDuplicateImmediateAsync(AudioPlaybackRequest request)
    {
        AudioPlaybackState currentState;
        bool isSameRequest;

        lock (_syncRoot)
        {
            currentState = _currentState;
            isSameRequest = _currentRequest?.Key == request.Key;
        }

        if (!isSameRequest)
        {
            return false;
        }

        if (currentState == AudioPlaybackState.Paused)
        {
            await ResumeAsync();
            return true;
        }

        return currentState is AudioPlaybackState.Preparing or AudioPlaybackState.Playing;
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            await _queueSignal.WaitAsync();

            if (_disposed)
            {
                return;
            }

            AudioPlaybackRequest? request = null;

            lock (_syncRoot)
            {
                if (_queue.Count > 0)
                {
                    request = _queue.First!.Value;
                    _queue.RemoveFirst();
                    _pendingKeys.Remove(request.Key);
                    _currentRequest = request;
                }
            }

            if (request is null)
            {
                continue;
            }

            try
            {
                await ExecuteRequestAsync(request);
                RaiseStateChanged(AudioPlaybackState.Completed, request, "Da phat xong thuyet minh.");
            }
            catch (OperationCanceledException)
            {
                // Stop, pause or interruption paths cancel the active playback.
            }
            catch (Exception ex)
            {
                RaiseStateChanged(AudioPlaybackState.Error, request, ex.Message);
            }
            finally
            {
                ReleasePlaybackResources();
                _releasePlatformPlaybackAccess();

                lock (_syncRoot)
                {
                    if (_currentRequest?.Key == request.Key)
                    {
                        _currentRequest = null;
                    }

                    if (_queue.Count == 0)
                    {
                        _currentState = AudioPlaybackState.Idle;
                    }
                }

                if (!HasPendingItems())
                {
                    RaiseStateChanged(AudioPlaybackState.Idle, request, "Hang cho audio dang trong.");
                }
            }
        }
    }

    private bool HasPendingItems()
    {
        lock (_syncRoot)
        {
            return _queue.Count > 0 || _currentRequest is not null;
        }
    }

    private async Task ExecuteRequestAsync(AudioPlaybackRequest request)
    {
        lock (_syncRoot)
        {
            _currentState = AudioPlaybackState.Preparing;
        }

        RaiseStateChanged(AudioPlaybackState.Preparing, request, BuildPreparingMessage(request));
        var playbackCts = new CancellationTokenSource();
        lock (_syncRoot)
        {
            _currentPlaybackCts = playbackCts;
        }

        await _requestPlatformPlaybackAccessAsync(playbackCts.Token);

        if (request.ContentType == AudioPlaybackContentType.AudioFile)
        {
            await PlayAudioFileAsync(request, playbackCts.Token);
            return;
        }

        lock (_syncRoot)
        {
            _currentState = AudioPlaybackState.Playing;
        }

        RaiseStateChanged(AudioPlaybackState.Playing, request, BuildPlayingMessage(request));
        await TextToSpeech.Default.SpeakAsync(request.TtsText!, cancelToken: playbackCts.Token);
    }

    private async Task PlayAudioFileAsync(AudioPlaybackRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.LocalAudioPath) && File.Exists(request.LocalAudioPath))
        {
            _currentStream = File.OpenRead(request.LocalAudioPath);
            _player = _audioManager.CreatePlayer(_currentStream);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.AudioUrl))
            {
                throw new InvalidOperationException("Dia diem nay chua co audio thuyet minh.");
            }

            await using var stream = await _httpClient.GetStreamAsync(AppConfig.NormalizeUrl(request.AudioUrl), cancellationToken);
            _currentBuffer = new MemoryStream();
            await stream.CopyToAsync(_currentBuffer, cancellationToken);
            _currentBuffer.Position = 0;
            _player = _audioManager.CreatePlayer(_currentBuffer);
        }

        if (_player is null)
        {
            throw new InvalidOperationException("Khong tao duoc audio player.");
        }

        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPlaybackEnded(object? sender, EventArgs args) => completionSource.TrySetResult();

        _player.PlaybackEnded += OnPlaybackEnded;

        try
        {
            cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
            lock (_syncRoot)
            {
                _currentState = AudioPlaybackState.Playing;
            }

            RaiseStateChanged(AudioPlaybackState.Playing, request, BuildPlayingMessage(request));
            _player.Play();
            await completionSource.Task;
        }
        finally
        {
            _player.PlaybackEnded -= OnPlaybackEnded;
        }
    }

    private async Task StopInternalAsync(bool clearQueue, AudioPlaybackState stopState, string message, bool raiseEvent = true)
    {
        AudioPlaybackRequest? interruptedRequest;

        lock (_syncRoot)
        {
            interruptedRequest = _currentRequest;

            if (clearQueue)
            {
                _queue.Clear();
                _pendingKeys.Clear();
            }

            _currentPlaybackCts?.Cancel();
            _currentState = stopState;
        }

        _player?.Stop();
        ReleasePlaybackResources();
        _releasePlatformPlaybackAccess();

        if (raiseEvent && interruptedRequest is not null)
        {
            RaiseStateChanged(stopState, interruptedRequest, message);
        }

        await Task.CompletedTask;
    }

    private void ReleasePlaybackResources()
    {
        lock (_syncRoot)
        {
            _currentPlaybackCts?.Dispose();
            _currentPlaybackCts = null;
            _player?.Dispose();
            _player = null;
            _currentBuffer?.Dispose();
            _currentBuffer = null;
            _currentStream?.Dispose();
            _currentStream = null;
        }
    }

    private void NotifyPlatformInterruption(string reason)
    {
        _ = HandlePlatformInterruptionAsync(reason);
    }

    private async Task HandlePlatformInterruptionAsync(string reason)
    {
        var request = GetCurrentRequestSnapshot();
        await StopInternalAsync(
            true,
            AudioPlaybackState.Interrupted,
            "Da dung audio do nguon am thanh khac chen vao.",
            raiseEvent: false);
        if (request is not null)
        {
            RaiseStateChanged(AudioPlaybackState.Interrupted, request, "Da dung audio do nguon am thanh khac chen vao.");
        }
    }

    private AudioPlaybackRequest? GetCurrentRequestSnapshot()
    {
        lock (_syncRoot)
        {
            return _currentRequest;
        }
    }

    private void RaiseStateChanged(AudioPlaybackState state, AudioPlaybackRequest? request, string message)
    {
        if (request is null)
        {
            return;
        }

        PlaybackStateChanged?.Invoke(
            this,
            new AudioPlaybackStateChangedEventArgs(
                state,
                request.PoiId,
                request.Language,
                request.Source,
                request.ContentType,
                request.Title,
                message));
    }

    private static string BuildPreparingMessage(AudioPlaybackRequest request)
    {
        return request.ContentType == AudioPlaybackContentType.AudioFile
            ? "Dang chuan bi audio..."
            : "Dang chuan bi TTS...";
    }

    private static string BuildPlayingMessage(AudioPlaybackRequest request)
    {
        if (request.ContentType == AudioPlaybackContentType.TextToSpeech)
        {
            return "Dang doc mo ta bang TTS.";
        }

        return string.IsNullOrWhiteSpace(request.LocalAudioPath)
            ? "Dang phat audio online."
            : "Dang phat audio offline.";
    }

    private sealed record AudioPlaybackRequest(
        string Key,
        string? PoiId,
        string Language,
        string Source,
        string? Title,
        AudioPlaybackContentType ContentType,
        string? AudioUrl = null,
        string? LocalAudioPath = null,
        string? TtsText = null)
    {
        public static AudioPlaybackRequest CreateAudio(
            string? poiId,
            string language,
            string source,
            string? title,
            string? audioUrl,
            string? localAudioPath)
        {
            return new AudioPlaybackRequest(
                BuildKey(poiId, language, AudioPlaybackContentType.AudioFile),
                poiId,
                language,
                source,
                title,
                AudioPlaybackContentType.AudioFile,
                audioUrl,
                localAudioPath);
        }

        public static AudioPlaybackRequest CreateTts(
            string? poiId,
            string language,
            string source,
            string? title,
            string text)
        {
            return new AudioPlaybackRequest(
                BuildKey(poiId, language, AudioPlaybackContentType.TextToSpeech),
                poiId,
                language,
                source,
                title,
                AudioPlaybackContentType.TextToSpeech,
                TtsText: text);
        }

        private static string BuildKey(string? poiId, string language, AudioPlaybackContentType contentType)
        {
            return $"{poiId ?? "global"}:{language}:{contentType}";
        }
    }
}
