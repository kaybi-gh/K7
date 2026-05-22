using System.Globalization;
using System.Text.RegularExpressions;
using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Uses FFmpeg silencedetect filter to determine fade-in/fade-out durations.
/// FadeIn: time from track start to end of first silence period.
/// FadeOut: time from start of last silence period to track end.
/// Threshold: -40 dB (silence floor for MixRamp).
/// </summary>
public partial class FfmpegFadeAnalyzer(ILogger<FfmpegFadeAnalyzer> logger) : IFadeAnalyzer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);
    private const double SilenceThresholdDb = -40.0;
    private const double MinSilenceDuration = 0.5;

    public async Task<FadeAnalysisResult?> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Fade analysis skipped: file not found at '{FilePath}'", filePath);
            return null;
        }

        try
        {
            var duration = await GetDurationAsync(filePath, cancellationToken);
            if (duration is null or <= 0)
                return null;

            var silencePeriods = await DetectSilenceAsync(filePath, cancellationToken);

            var fadeIn = ComputeFadeIn(silencePeriods);
            var fadeOut = ComputeFadeOut(silencePeriods, duration.Value);

            return new FadeAnalysisResult(fadeIn, fadeOut);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fade analysis failed for '{FilePath}'", filePath);
            return null;
        }
    }

    private async Task<double?> GetDurationAsync(string filePath, CancellationToken cancellationToken)
    {
        double? duration = null;

        await SafeProcessRunner.RunAsync(
            "ffprobe",
            $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
            onStdout: line =>
            {
                if (double.TryParse(line.Trim(), CultureInfo.InvariantCulture, out var d))
                    duration = d;
            },
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        return duration;
    }

    private async Task<List<SilencePeriod>> DetectSilenceAsync(string filePath, CancellationToken cancellationToken)
    {
        var periods = new List<SilencePeriod>();
        double? currentStart = null;

        await SafeProcessRunner.RunAsync(
            "ffmpeg",
            $"-i \"{filePath}\" -af silencedetect=noise={SilenceThresholdDb}dB:d={MinSilenceDuration} -f null -",
            onStderr: line =>
            {
                var startMatch = SilenceStartRegex().Match(line);
                if (startMatch.Success && double.TryParse(startMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var start))
                {
                    currentStart = start;
                    return;
                }

                var endMatch = SilenceEndRegex().Match(line);
                if (endMatch.Success && double.TryParse(endMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var end))
                {
                    if (currentStart.HasValue)
                    {
                        periods.Add(new SilencePeriod(currentStart.Value, end));
                        currentStart = null;
                    }
                }
            },
            timeout: Timeout,
            cancellationToken: cancellationToken);

        return periods;
    }

    private static double ComputeFadeIn(List<SilencePeriod> periods)
    {
        // FadeIn = end of initial silence (if track starts with silence)
        if (periods.Count > 0 && periods[0].Start < 0.5)
            return periods[0].End;

        return 0;
    }

    private static double ComputeFadeOut(List<SilencePeriod> periods, double trackDuration)
    {
        // FadeOut = duration from start of final silence to track end
        if (periods.Count == 0)
            return 0;

        var lastPeriod = periods[^1];
        var distanceFromEnd = trackDuration - lastPeriod.Start;

        // Only consider it a fade-out if the silence is within the last 15s
        if (distanceFromEnd > 15.0)
            return 0;

        return distanceFromEnd;
    }

    private sealed record SilencePeriod(double Start, double End);

    [GeneratedRegex(@"silence_start:\s*([\d.]+)")]
    private static partial Regex SilenceStartRegex();

    [GeneratedRegex(@"silence_end:\s*([\d.]+)")]
    private static partial Regex SilenceEndRegex();
}
