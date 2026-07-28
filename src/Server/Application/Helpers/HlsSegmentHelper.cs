using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

public static class HlsSegmentHelper
{
    public const string FallbackTranscodingVideoCodec = "h264";

    public const int TargetSegmentDurationMs = Hls.TargetSegmentDurationMs;

    public const double TargetSegmentDurationSeconds = Hls.TargetSegmentDurationSeconds;

    public static async Task<IReadOnlyList<HlsSegment>> LoadSegmentsAsync(
        IApplicationDbContext context,
        Guid indexedFileId,
        CancellationToken cancellationToken = default)
    {
        return await context.HlsSegments
            .Where(s => s.IndexedFileId == indexedFileId)
            .OrderBy(s => s.Number)
            .ToListAsync(cancellationToken);
    }

    public static async Task<bool> HasSegmentsAsync(
        IApplicationDbContext context,
        Guid indexedFileId,
        CancellationToken cancellationToken = default)
    {
        return await context.HlsSegments
            .AnyAsync(s => s.IndexedFileId == indexedFileId, cancellationToken);
    }

    /// <summary>
    /// Video + demuxed audio streaming timeline. Prefer keyframe-aligned DB segments;
    /// fall back to equal-length only when missing.
    /// </summary>
    public static List<HlsSegment> ResolveVideoStreamingSegments(
        IReadOnlyList<HlsSegment> keyframeSegments,
        long totalDurationMs)
    {
        if (keyframeSegments.Count > 0)
            return keyframeSegments.OrderBy(s => s.Number).ToList();

        return ComputeEqualLengthHlsSegments(totalDurationMs);
    }

    /// <summary>
    /// Alias for shared A/V timeline resolution (video keyframes drive audio cuts too).
    /// </summary>
    public static List<HlsSegment> ResolveStreamingSegments(
        IReadOnlyList<HlsSegment> keyframeSegments,
        long totalDurationMs) =>
        ResolveVideoStreamingSegments(keyframeSegments, totalDurationMs);

    public static double[] ToDurationSeconds(IReadOnlyList<HlsSegment> segments) =>
        segments.Select(s => s.Duration / 1000.0).ToArray();

    /// <summary>
    /// Snap to the previous boundary on an arbitrary segment duration timeline.
    /// </summary>
    public static double AlignToPreviousSegmentBoundary(
        double positionSeconds,
        IReadOnlyList<double> segmentDurationsSeconds)
    {
        if (positionSeconds <= 0 || segmentDurationsSeconds.Count == 0)
            return 0;

        var cursor = 0.0;
        foreach (var duration in segmentDurationsSeconds)
        {
            var next = cursor + duration;
            // Still inside this segment (not yet at/after the next boundary).
            if (positionSeconds < next - 0.0005)
                return cursor;
            cursor = next;
        }

        return cursor;
    }

    /// <summary>
    /// Snap to the previous boundary on an equal-length fallback grid when no duration list is available.
    /// </summary>
    public static double AlignToPreviousSegmentBoundary(double positionSeconds)
    {
        if (positionSeconds <= 0)
            return 0;

        return Math.Floor(positionSeconds / TargetSegmentDurationSeconds) * TargetSegmentDurationSeconds;
    }

    public static List<HlsSegment> ComputeEqualLengthHlsSegments(
        long totalDurationMs,
        int desiredSegmentLengthMs = TargetSegmentDurationMs)
    {
        var segments = new List<HlsSegment>();
        long offset = 0;
        var index = 0;
        while (offset < totalDurationMs)
        {
            var duration = Math.Min(desiredSegmentLengthMs, totalDurationMs - offset);
            segments.Add(new HlsSegment
            {
                Number = index,
                StartTimestamp = offset,
                Duration = duration
            });
            offset += desiredSegmentLengthMs;
            index++;
        }

        return segments;
    }

    public static async Task QueueSegmentComputationIfMissingAsync(
        ISender sender,
        Guid indexedFileId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "HLS segments not available for IndexedFile {Id}, queuing segmentation",
            indexedFileId);

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new ComputeHlsSegmentsCommand
            {
                Id = indexedFileId,
                SegmentsDuration = TimeSpan.FromMilliseconds(TargetSegmentDurationMs)
            },
            TargetEntityId = indexedFileId,
            TargetEntityTypeName = nameof(IndexedFile),
            Lane = BackgroundTaskLane.FfmpegPrepare,
            WorkClass = BackgroundTaskWorkClass.Prepare,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 5
        }, cancellationToken);
    }
}
