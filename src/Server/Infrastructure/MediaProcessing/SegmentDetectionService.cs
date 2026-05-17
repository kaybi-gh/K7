using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FFMpegCore;
using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public partial class SegmentDetectionService(ILogger<SegmentDetectionService> logger) : ISegmentDetectionService
{
    public async Task<IReadOnlyList<SilencePeriod>> DetectSilenceAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SilencePeriod>();
        var stderrLines = new List<string>();

        var args = $"-vn -sn -dn " +
                   $"-ss {startSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-i \"{filePath}\" " +
                   $"-t {durationSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-af \"silencedetect=noise=-50dB:duration=0.1\" -f null -";

        var exitCode = await SafeProcessRunner.RunAsync(
            GlobalFFOptions.GetFFMpegBinaryPath(),
            args,
            onStderr: line => stderrLines.Add(line),
            timeout: TimeSpan.FromSeconds(60),
            cancellationToken: cancellationToken);

        if (exitCode != 0)
        {
            logger.LogDebug("Silence detection failed for '{FilePath}' with exit code {ExitCode}", filePath, exitCode);
            return results;
        }

        double? currentStart = null;
        foreach (var line in stderrLines)
        {
            var startMatch = SilenceStartRegex().Match(line);
            if (startMatch.Success &&
                double.TryParse(startMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var silStart))
            {
                currentStart = startSeconds + silStart;
                continue;
            }

            var endMatch = SilenceEndRegex().Match(line);
            if (endMatch.Success && currentStart is not null &&
                double.TryParse(endMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var silEnd))
            {
                results.Add(new SilencePeriod(currentStart.Value, startSeconds + silEnd));
                currentStart = null;
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<double>> DetectBlackFrameTimestampsAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var results = new List<double>();
        var stderrLines = new List<string>();

        var args = $"-ss {startSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-i \"{filePath}\" " +
                   $"-t {durationSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-an -dn -sn -vf \"blackframe=amount=50:threshold=28\" -f null -";

        var exitCode = await SafeProcessRunner.RunAsync(
            GlobalFFOptions.GetFFMpegBinaryPath(),
            args,
            onStderr: line => stderrLines.Add(line),
            timeout: TimeSpan.FromSeconds(60),
            cancellationToken: cancellationToken);

        if (exitCode != 0)
            return results;

        foreach (var line in stderrLines)
        {
            var match = BlackFrameRegex().Match(line);
            if (!match.Success)
                continue;

            if (double.TryParse(match.Groups["pblack"].Value, CultureInfo.InvariantCulture, out var percentage) &&
                double.TryParse(match.Groups["time"].Value, CultureInfo.InvariantCulture, out var timestamp) &&
                percentage >= 85)
            {
                results.Add(startSeconds + timestamp);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<double>> DetectKeyframeTimestampsAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var results = new List<double>();
        var stderrLines = new List<string>();

        var args = $"-skip_frame nokey " +
                   $"-ss {startSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-i \"{filePath}\" " +
                   $"-t {durationSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-an -dn -sn -vf \"showinfo\" -f null -";

        var exitCode = await SafeProcessRunner.RunAsync(
            GlobalFFOptions.GetFFMpegBinaryPath(),
            args,
            onStderr: line => stderrLines.Add(line),
            timeout: TimeSpan.FromSeconds(60),
            cancellationToken: cancellationToken);

        if (exitCode != 0)
            return results;

        foreach (var line in stderrLines)
        {
            var match = PtsTimeRegex().Match(line);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var pts))
            {
                results.Add(startSeconds + pts);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<double>> GetChapterTimesAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stdoutLines = new List<string>();
            var ffprobePath = GlobalFFOptions.GetFFProbeBinaryPath();

            var exitCode = await SafeProcessRunner.RunAsync(
                ffprobePath,
                $"-v quiet -print_format json -show_chapters \"{filePath}\"",
                onStdout: line => stdoutLines.Add(line),
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            if (exitCode != 0)
                return [];

            var json = string.Join("", stdoutLines);
            using var doc = JsonDocument.Parse(json);

            var results = new List<double>();
            if (doc.RootElement.TryGetProperty("chapters", out var chapters))
            {
                foreach (var chapter in chapters.EnumerateArray())
                {
                    if (chapter.TryGetProperty("start_time", out var startTime) &&
                        double.TryParse(startTime.GetString(), CultureInfo.InvariantCulture, out var time))
                    {
                        results.Add(time);
                    }
                }
            }

            return results.OrderBy(t => t).ToList();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to read chapters from '{FilePath}'", filePath);
            return [];
        }
    }

    [GeneratedRegex(@"silence_start:\s*(-?[\d.]+)")]
    private static partial Regex SilenceStartRegex();

    [GeneratedRegex(@"silence_end:\s*(-?[\d.]+)")]
    private static partial Regex SilenceEndRegex();

    [GeneratedRegex(@"blackframe.*pblack:(?<pblack>[\d.]+).*t:(?<time>[\d.]+)")]
    private static partial Regex BlackFrameRegex();

    [GeneratedRegex(@"pts_time:([\d.]+)")]
    private static partial Regex PtsTimeRegex();
}
