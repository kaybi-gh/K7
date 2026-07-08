using System.Globalization;
using FFMpegCore;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public class EpisodeStillGenerator(
    ISegmentDetectionService segmentDetectionService,
    IImageProcessor imageProcessor,
    ILogger<EpisodeStillGenerator> logger) : IEpisodeStillGenerator
{
    private const double KeyframeScanBeforeSeconds = 30;
    private const double KeyframeScanDurationSeconds = 60;
    private const double BlackFrameScanDurationSeconds = 30;

    public async Task<EpisodeStillGenerationResult> GenerateAsync(
        string videoFilePath,
        string outputFilePath,
        double durationSeconds,
        double? introEndSeconds,
        CancellationToken cancellationToken = default)
    {
        var preliminaryTarget = EpisodeStillTimestampSelector.SelectTimestamp(
            durationSeconds,
            introEndSeconds,
            [],
            []);

        var (windowStart, windowEnd) = EpisodeStillTimestampSelector.GetContentWindow(durationSeconds, introEndSeconds);
        var scanStart = Math.Max(preliminaryTarget - KeyframeScanBeforeSeconds, windowStart);
        var scanDuration = Math.Min(KeyframeScanDurationSeconds, windowEnd - scanStart);

        IReadOnlyList<double> keyframes = scanDuration > 0
            ? await segmentDetectionService.DetectKeyframeTimestampsAsync(
                videoFilePath,
                scanStart,
                scanDuration,
                cancellationToken)
            : [];

        var blackFrameScanStart = Math.Max(preliminaryTarget - KeyframeScanBeforeSeconds, windowStart);
        var blackFrameScanDuration = Math.Min(BlackFrameScanDurationSeconds, windowEnd - blackFrameScanStart);

        IReadOnlyList<double> blackFrames = blackFrameScanDuration > 0
            ? await segmentDetectionService.DetectBlackFrameTimestampsAsync(
                videoFilePath,
                blackFrameScanStart,
                blackFrameScanDuration,
                cancellationToken)
            : [];

        var timestamp = EpisodeStillTimestampSelector.SelectTimestamp(
            durationSeconds,
            introEndSeconds,
            keyframes,
            blackFrames);

        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        logger.LogInformation(
            "Extracting episode still from {FilePath} at {TimestampSeconds}s",
            videoFilePath,
            timestamp);

        await FFMpegArguments
            .FromFileInput(videoFilePath, verifyExists: false, options => options
                .Seek(TimeSpan.FromSeconds(timestamp)))
            .OutputToFile(outputFilePath, overwrite: true, options => options
                .WithFrameOutputCount(1)
                .WithCustomArgument("-q:v 2"))
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromMinutes(2).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);

        if (!File.Exists(outputFilePath))
            throw new InvalidOperationException($"Failed to extract still frame at {timestamp.ToString(CultureInfo.InvariantCulture)}s.");

        var dimensions = imageProcessor.TryGetImageDimensions(outputFilePath);
        return new EpisodeStillGenerationResult(
            outputFilePath,
            dimensions?.Width ?? 0,
            dimensions?.Height ?? 0,
            timestamp);
    }
}
