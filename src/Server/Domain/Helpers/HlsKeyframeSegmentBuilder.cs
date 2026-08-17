using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;

namespace K7.Server.Domain.Helpers;

/// <summary>
/// Builds the shared A/V HLS timeline from source keyframe timestamps (ms).
/// </summary>
public static class HlsKeyframeSegmentBuilder
{
    /// <summary>
    /// One segment per keyframe, collapsing bursts shorter than
    /// <paramref name="minSegmentDurationMs"/>.
    /// </summary>
    public static List<HlsSegment> BuildFromTimestamps(
        IReadOnlyList<long> keyframeTimestampsMs,
        long totalVideoDurationMs,
        Guid fileMetadataId,
        Guid indexedFileId,
        long minSegmentDurationMs = Hls.MinKeyframeSegmentDurationMs)
    {
        if (keyframeTimestampsMs.Count == 0)
        {
            // Start of file is always a decode point. An empty probe is a read miss, not a
            // keyframe-less video; emit one segment covering the whole duration.
            if (totalVideoDurationMs <= 0)
                return [];

            keyframeTimestampsMs = [0L];
        }

        if (totalVideoDurationMs <= 0)
            return [];

        var segments = new List<HlsSegment>();
        long segmentStart = 0;

        for (var i = 1; i < keyframeTimestampsMs.Count; i++)
        {
            var segmentEnd = keyframeTimestampsMs[i];
            if (segmentEnd - segmentStart < minSegmentDurationMs)
                continue;

            segments.Add(new HlsSegment
            {
                FileMetadataId = fileMetadataId,
                IndexedFileId = indexedFileId,
                Number = segments.Count,
                StartTimestamp = segmentStart,
                Duration = segmentEnd - segmentStart
            });
            segmentStart = segmentEnd;
        }

        var lastSegmentDuration = totalVideoDurationMs - segmentStart;
        if (lastSegmentDuration <= 0 && segments.Count > 0)
            return segments;

        if (lastSegmentDuration < minSegmentDurationMs && segments.Count > 0)
        {
            var previousSegment = segments[^1];
            previousSegment.Duration = totalVideoDurationMs - previousSegment.StartTimestamp;
        }
        else if (lastSegmentDuration > 0)
        {
            segments.Add(new HlsSegment
            {
                FileMetadataId = fileMetadataId,
                IndexedFileId = indexedFileId,
                Number = segments.Count,
                StartTimestamp = segmentStart,
                Duration = lastSegmentDuration
            });
        }

        return segments;
    }
}
