namespace Quan4CulinaryTourism.Mobile.Services;

public enum AudioPlaybackState
{
    Idle,
    Queued,
    Preparing,
    Playing,
    Paused,
    Stopped,
    Completed,
    Interrupted,
    Error
}

public enum AudioPlaybackContentType
{
    AudioFile,
    TextToSpeech
}

public sealed class AudioPlaybackStateChangedEventArgs : EventArgs
{
    public AudioPlaybackStateChangedEventArgs(
        AudioPlaybackState state,
        string? poiId,
        string language,
        string source,
        AudioPlaybackContentType contentType,
        string? title,
        string message)
    {
        State = state;
        PoiId = poiId;
        Language = language;
        Source = source;
        ContentType = contentType;
        Title = title;
        Message = message;
    }

    public AudioPlaybackState State { get; }

    public string? PoiId { get; }

    public string Language { get; }

    public string Source { get; }

    public AudioPlaybackContentType ContentType { get; }

    public string? Title { get; }

    public string Message { get; }
}
