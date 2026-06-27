namespace Quan4CulinaryTourism.Mobile.Services;

public partial class AudioPlayerService
{
#if !ANDROID && !IOS
    partial void ConfigurePlatformAudioHooks()
    {
    }
#endif
}
