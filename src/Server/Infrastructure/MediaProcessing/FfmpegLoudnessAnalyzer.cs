using System.Globalization;
using System.Text.RegularExpressions;
using K7.Server.Application.Common.Configuration;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.MediaProcessing;

public partial class FfmpegLoudnessAnalyzer : ILoudnessAnalyzer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly PathsConfiguration _paths;
    private readonly ILogger<FfmpegLoudnessAnalyzer> _logger;

    public FfmpegLoudnessAnalyzer(IOptions<PathsConfiguration> paths, ILogger<FfmpegLoudnessAnalyzer> logger)
    {
        _paths = paths.Value;
        _logger = logger;
    }

    public async Task<double?> AnalyzeLufsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Loudness analysis skipped: file not found at '{FilePath}'", filePath);
            return null;
        }

        double? integratedLufs = null;
        var ffmpegPath = string.IsNullOrEmpty(_paths.FFMpegBinaryFolder)
            ? "ffmpeg"
            : Path.Combine(_paths.FFMpegBinaryFolder, "ffmpeg");

        try
        {
            var exitCode = await SafeProcessRunner.RunAsync(
                ffmpegPath,
                $"-i \"{filePath}\" -af ebur128 -f null -",
                onStderr: line =>
                {
                    var match = IntegratedLoudnessRegex().Match(line);
                    if (match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var lufs))
                    {
                        integratedLufs = lufs;
                    }
                },
                timeout: Timeout,
                cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                _logger.LogWarning("ffmpeg ebur128 exited with code {ExitCode} for '{FilePath}'", exitCode, filePath);
                return null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Loudness analysis failed for '{FilePath}'", filePath);
            return null;
        }

        return integratedLufs;
    }

    [GeneratedRegex(@"I:\s+(-?\d+\.?\d*)\s+LUFS")]
    private static partial Regex IntegratedLoudnessRegex();
}
