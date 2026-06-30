using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Quan4CulinaryTourism.Api.Database;

namespace Quan4CulinaryTourism.Api.Services;

public class AiChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly AiSettings _settings;
    private readonly ILogger<AiChatClient> _logger;

    public AiChatClient(HttpClient httpClient, IOptions<AiSettings> settings, ILogger<AiChatClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public bool CanUseAi() =>
        _settings.Enabled &&
        !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
        !string.IsNullOrWhiteSpace(_settings.BaseUrl) &&
        !string.IsNullOrWhiteSpace(_settings.Model);

    public async Task<string?> GenerateReplyAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (!CanUseAi())
        {
            return null;
        }

        if (!TryEnsureBaseAddress())
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model = _settings.Model.Trim(),
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.2
            }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_settings.TimeoutSeconds, 5, 120)));

            using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            var raw = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI request failed with status code {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var content = ExtractMessageContent(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("AI response did not contain a usable message.");
                return null;
            }

            return content;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AI request timed out after {TimeoutSeconds}s.", _settings.TimeoutSeconds);
            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "AI response JSON could not be parsed.");
            return null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "AI request failed.");
            return null;
        }
    }

    private bool TryEnsureBaseAddress()
    {
        var normalizedBaseUrl = NormalizeBaseUrl(_settings.BaseUrl);
        if (!Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("AI base URL is invalid.");
            return false;
        }

        if (_httpClient.BaseAddress != uri)
        {
            _httpClient.BaseAddress = uri;
        }

        return true;
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1"
            : baseUrl.Trim();

        return normalized.EndsWith("/", StringComparison.Ordinal)
            ? normalized
            : normalized + "/";
    }

    private static string? ExtractMessageContent(string rawResponse)
    {
        using var document = JsonDocument.Parse(rawResponse);
        if (!document.RootElement.TryGetProperty("choices", out var choicesElement) ||
            choicesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        using var enumerator = choicesElement.EnumerateArray();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var firstChoice = enumerator.Current;
        if (!firstChoice.TryGetProperty("message", out var messageElement) ||
            !messageElement.TryGetProperty("content", out var contentElement))
        {
            return null;
        }

        return ReadContent(contentElement);
    }

    private static string? ReadContent(JsonElement contentElement)
    {
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString();
        }

        if (contentElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var item in contentElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Append(item.GetString());
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (item.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
            {
                builder.Append(textElement.GetString());
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
