using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Quan4CulinaryTourism.Api.Database;

namespace Quan4CulinaryTourism.Api.Services;

public class PythonTranslationService
{
    private const string ScriptPath = "tools/translate_localization.py";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TextToSpeechSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PythonTranslationService> _logger;

    public PythonTranslationService(
        IOptions<TextToSpeechSettings> settings,
        IWebHostEnvironment environment,
        ILogger<PythonTranslationService> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    public bool IsAvailable()
    {
        if (string.IsNullOrWhiteSpace(_settings.PythonCommand))
        {
            return false;
        }

        return File.Exists(Path.Combine(_environment.ContentRootPath, ScriptPath));
    }

    public async Task<LocalizationTranslationPayload?> TranslateAsync(
        string sourceLang,
        string targetLang,
        LocalizationTranslationPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable())
        {
            return null;
        }

        var scriptPath = Path.Combine(_environment.ContentRootPath, ScriptPath);
        var tempInputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-translate.json");

        await File.WriteAllTextAsync(
            tempInputPath,
            JsonSerializer.Serialize(new PythonTranslationRequest
            {
                SourceLang = sourceLang,
                TargetLang = targetLang,
                Name = payload.Name,
                Description = payload.Description,
                TtsScript = payload.TtsScript ?? string.Empty
            }, JsonOptions),
            Utf8WithoutBom,
            cancellationToken);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _settings.PythonCommand,
                WorkingDirectory = _environment.ContentRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add(scriptPath);
        process.StartInfo.ArgumentList.Add("--input");
        process.StartInfo.ArgumentList.Add(tempInputPath);
        process.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        try
        {
            process.Start();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _settings.TimeoutSeconds)));

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var standardErrorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(standardOutput))
            {
                _logger.LogWarning(
                    "Python translation failed with exit code {ExitCode}. stdout: {StdOut}. stderr: {StdErr}",
                    process.ExitCode,
                    standardOutput,
                    standardError);
                return null;
            }

            var translated = JsonSerializer.Deserialize<LocalizationTranslationPayload>(standardOutput, JsonOptions);
            if (translated is null)
            {
                return null;
            }

            translated.Name = translated.Name?.Trim() ?? string.Empty;
            translated.Description = translated.Description?.Trim() ?? string.Empty;
            translated.TtsScript = translated.TtsScript?.Trim();

            if (string.IsNullOrWhiteSpace(translated.Name) || string.IsNullOrWhiteSpace(translated.Description))
            {
                return null;
            }

            return translated;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Python translation timed out after {TimeoutSeconds}s", _settings.TimeoutSeconds);
            return null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Python translation execution failed");
            return null;
        }
        finally
        {
            process.Dispose();
            TryDelete(tempInputPath);
        }
    }

    private static void TryDelete(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private sealed class PythonTranslationRequest
    {
        public string SourceLang { get; set; } = "vi";
        public string TargetLang { get; set; } = "en";
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TtsScript { get; set; } = string.Empty;
    }
}
