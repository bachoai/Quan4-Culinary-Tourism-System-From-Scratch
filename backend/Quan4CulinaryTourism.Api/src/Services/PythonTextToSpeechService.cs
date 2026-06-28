using System.Diagnostics;
using Microsoft.Extensions.Options;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Helpers;

namespace Quan4CulinaryTourism.Api.Services;

public class PythonTextToSpeechService
{
    private readonly TextToSpeechSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly FileUploadHelper _fileUploadHelper;
    private readonly ILogger<PythonTextToSpeechService> _logger;

    public PythonTextToSpeechService(
        IOptions<TextToSpeechSettings> settings,
        IWebHostEnvironment environment,
        FileUploadHelper fileUploadHelper,
        ILogger<PythonTextToSpeechService> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _fileUploadHelper = fileUploadHelper;
        _logger = logger;
    }

    public async Task<GeneratedAudioResult?> GenerateAudioAsync(
        string text,
        string? voiceHint = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var scriptPath = Path.Combine(_environment.ContentRootPath, _settings.ScriptPath);
        if (!File.Exists(scriptPath))
        {
            _logger.LogWarning("TTS script not found at {ScriptPath}", scriptPath);
            return null;
        }

        var (fullPath, publicUrl) = _fileUploadHelper.CreateManagedFilePath("audio", ".mp3");
        var resolvedVoice = string.IsNullOrWhiteSpace(voiceHint) ? _settings.DefaultVoice : voiceHint.Trim();
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
        process.StartInfo.ArgumentList.Add("--text");
        process.StartInfo.ArgumentList.Add(text);
        process.StartInfo.ArgumentList.Add("--output");
        process.StartInfo.ArgumentList.Add(fullPath);
        process.StartInfo.ArgumentList.Add("--voice");
        process.StartInfo.ArgumentList.Add(resolvedVoice);
        process.StartInfo.ArgumentList.Add("--rate");
        process.StartInfo.ArgumentList.Add(_settings.Rate);
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

            if (process.ExitCode != 0 || !File.Exists(fullPath))
            {
                TryDelete(fullPath);
                _logger.LogError(
                    "Python TTS failed with exit code {ExitCode}. stdout: {StdOut}. stderr: {StdErr}",
                    process.ExitCode,
                    standardOutput,
                    standardError);
                return null;
            }

            var fileInfo = new FileInfo(fullPath);
            return new GeneratedAudioResult(publicUrl, resolvedVoice, fileInfo.Length);
        }
        catch (OperationCanceledException)
        {
            TryDelete(fullPath);
            _logger.LogError("Python TTS timed out after {TimeoutSeconds}s", _settings.TimeoutSeconds);
            return null;
        }
        catch (Exception exception)
        {
            TryDelete(fullPath);
            _logger.LogError(exception, "Python TTS execution failed");
            return null;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void TryDelete(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}

public sealed record GeneratedAudioResult(string PublicUrl, string VoiceName, long FileSizeBytes);
