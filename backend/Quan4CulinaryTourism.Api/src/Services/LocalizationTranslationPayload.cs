namespace Quan4CulinaryTourism.Api.Services;

public sealed class LocalizationTranslationPayload
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? TtsScript { get; set; }
}
