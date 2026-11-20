using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using FFMpegCore;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.Files;
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

    public async Task<IReadOnlyList<CreditsFrameAnalyzer.FrameStats>?> SampleCreditsFramesAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default)
    {
        // Credits that count reach the end of the file, so the span is decoded backwards from
        // there, one chunk at a time, and no further than the credits go. Chunks start on whole
        // intervals so that they share one sampling grid.
        var interval = CreditsFrameAnalyzer.SampleIntervalSeconds;
        var totalFrames = (int)(durationSeconds / interval);
        var chunkFrames = (int)(CreditsFrameAnalyzer.ChunkSeconds / interval);
        var decoded = new List<CreditsFrameAnalyzer.FrameStats>();
        var firstIndex = totalFrames;

        while (firstIndex > 0)
        {
            var chunkIndex = Math.Max(0, firstIndex - chunkFrames);
            var chunkSeconds = firstIndex == totalFrames
                ? durationSeconds - chunkIndex * interval
                : (firstIndex - chunkIndex) * interval;

            var chunk = await SampleFramesAsync(
                filePath, startSeconds + chunkIndex * interval, chunkSeconds, cancellationToken);
            if (chunk is null)
                return null;

            decoded.InsertRange(0, CreditsFrameAnalyzer.FitToSpan(chunk, chunkSeconds));
            firstIndex = chunkIndex;

            if (!CreditsFrameAnalyzer.MayStartEarlier(decoded))
                break;
        }

        // Slots before the decoded part stay empty: they are neither cards nor blank.
        var frames = new List<CreditsFrameAnalyzer.FrameStats>(totalFrames);
        frames.AddRange(Enumerable.Repeat(default(CreditsFrameAnalyzer.FrameStats), firstIndex));
        frames.AddRange(decoded);
        return frames;
    }

    private async Task<List<CreditsFrameAnalyzer.FrameStats>?> SampleFramesAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        var interval = CreditsFrameAnalyzer.SampleIntervalSeconds.ToString("F3", CultureInfo.InvariantCulture);
        var args = $"-v error -ss {startSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-i \"{filePath}\" " +
                   $"-t {durationSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                   $"-an -dn -sn -map 0:v:0 " +
                   $"-vf \"fps=1/{interval},scale={CreditsFrameAnalyzer.FrameWidth}:{CreditsFrameAnalyzer.FrameHeight}\" " +
                   "-pix_fmt rgb24 -f rawvideo -";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GlobalFFOptions.GetFFMpegBinaryPath(),
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                logger.LogWarning("Failed to start FFmpeg for credits frame sampling");
                return null;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(MediaAnalysisTimeouts.CreditsFrameSamplingSeconds));

            try
            {
                var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
                var frames = await ReadFrameStatsAsync(process.StandardOutput.BaseStream, timeoutCts.Token);
                await process.WaitForExitAsync(timeoutCts.Token);
                await stderrTask;

                if (process.ExitCode != 0)
                {
                    logger.LogDebug(
                        "Credits frame sampling failed for '{FilePath}' with exit code {ExitCode}",
                        filePath,
                        process.ExitCode);
                    return null;
                }

                return frames;
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (cancellationToken.IsCancellationRequested)
                    throw;

                logger.LogWarning("Credits frame sampling timed out for '{FilePath}'", filePath);
                return null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Credits frame sampling failed for '{FilePath}'", filePath);
            return null;
        }
    }

    private static async Task<List<CreditsFrameAnalyzer.FrameStats>> ReadFrameStatsAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var frames = new List<CreditsFrameAnalyzer.FrameStats>();
        var buffer = new byte[CreditsFrameAnalyzer.FrameBytes];

        while (true)
        {
            var read = await stream.ReadAtLeastAsync(
                buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
            if (read < buffer.Length)
                break;

            frames.Add(CreditsFrameAnalyzer.ComputeStats(buffer));
        }

        return frames;
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogDebug(ex, "Credits frame sampling process kill failed");
        }
    }

    public async Task<IReadOnlyList<double>> GetChapterTimesAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var chapters = await GetChaptersAsync(filePath, cancellationToken);
        return chapters.Select(c => c.StartSeconds).ToList();
    }

    public async Task<IReadOnlyList<ChapterMarker>> GetChaptersAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ChapterProbe.ReadAsync(filePath, cancellationToken);
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
