using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.MediaProcessing;

public class EssentiaAudioAnalyzer : IAudioAnalyzer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private const int AnalysisVersion = 1;

    private readonly PathsConfiguration _paths;
    private readonly ILogger<EssentiaAudioAnalyzer> _logger;
    private readonly Lazy<bool> _isAvailable;

    public EssentiaAudioAnalyzer(IOptions<PathsConfiguration> paths, ILogger<EssentiaAudioAnalyzer> logger)
    {
        _paths = paths.Value;
        _logger = logger;
        _isAvailable = new Lazy<bool>(CheckAvailability);
    }

    public bool IsAvailable => _isAvailable.Value;

    public async Task<AudioAnalysis?> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogDebug("Audio analysis skipped: Essentia is not available");
            return null;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Audio analysis skipped: file not found at '{FilePath}'", filePath);
            return null;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"essentia_{Guid.NewGuid():N}.json");

        try
        {
            var exitCode = await SafeProcessRunner.RunAsync(
                _paths.EssentiaBinaryPath,
                $"\"{filePath}\" \"{outputPath}\"",
                onStderr: line => _logger.LogDebug("[essentia] {Line}", line),
                timeout: Timeout,
                cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                _logger.LogWarning("Essentia exited with code {ExitCode} for '{FilePath}'", exitCode, filePath);
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogWarning("Essentia produced no output for '{FilePath}'", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(outputPath, cancellationToken);
            return ParseEssentiaOutput(json);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Essentia timed out after {Timeout} for '{FilePath}'", Timeout, filePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Essentia analysis failed for '{FilePath}'", filePath);
            return null;
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    private AudioAnalysis? ParseEssentiaOutput(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var analysis = new AudioAnalysis
            {
                Bpm = GetDouble(root, "rhythm", "bpm"),
                MusicalKey = GetMusicalKey(root),
                LoudnessLufs = GetDouble(root, "lowlevel", "loudness_ebu128", "integrated"),
                LoudnessRange = GetDouble(root, "lowlevel", "loudness_ebu128", "loudness_range"),
                Energy = GetDouble(root, "lowlevel", "average_loudness"),
                Danceability = GetHighLevelValue(root, "danceability", "danceable"),
                Valence = GetHighLevelValue(root, "mood_happy", "happy"),
                AnalyzedAt = DateTime.UtcNow,
                AnalysisVersion = AnalysisVersion
            };

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Essentia JSON output");
            return null;
        }
    }

    /// <summary>
    /// Extracts musical key in format "C major" or "A minor" from Essentia's tonal analysis.
    /// </summary>
    private static string? GetMusicalKey(JsonElement root)
    {
        // Essentia uses several key estimation algorithms; Krumhansl is the most reliable
        string[] keyPaths = ["key_krumhansl", "key_edma", "key_temperley"];

        if (!root.TryGetProperty("tonal", out var tonal))
            return null;

        foreach (var keyPath in keyPaths)
        {
            if (tonal.TryGetProperty(keyPath, out var keyObj)
                && keyObj.TryGetProperty("key", out var key)
                && keyObj.TryGetProperty("scale", out var scale))
            {
                return $"{key.GetString()} {scale.GetString()}";
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts high-level classifier probability (0.0-1.0).
    /// Essentia format: { "highlevel": { "danceability": { "all": { "danceable": 0.7 } } } }
    /// </summary>
    private static double? GetHighLevelValue(JsonElement root, string category, string positiveLabel)
    {
        if (root.TryGetProperty("highlevel", out var hl)
            && hl.TryGetProperty(category, out var cat)
            && cat.TryGetProperty("all", out var all)
            && all.TryGetProperty(positiveLabel, out var value))
        {
            return value.GetDouble();
        }

        return null;
    }

    /// <summary>
    /// Navigates nested JSON properties to extract a double value.
    /// </summary>
    private static double? GetDouble(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind == JsonValueKind.Number ? current.GetDouble() : null;
    }

    private bool CheckAvailability()
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _paths.EssentiaBinaryPath,
                    Arguments = "--help",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000);
            _logger.LogInformation("Essentia audio analyzer is available at '{Path}'", _paths.EssentiaBinaryPath);
            return true;
        }
        catch
        {
            _logger.LogWarning("Essentia binary not found at '{Path}'. Audio analysis will be skipped", _paths.EssentiaBinaryPath);
            return false;
        }
    }
}
