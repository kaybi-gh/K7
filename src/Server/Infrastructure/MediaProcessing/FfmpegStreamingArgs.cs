using System.Globalization;
using K7.Server.Domain.Entities;

namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Pure ffmpeg argument contracts for keyframe-aligned streaming (no process launch).
/// Video mid-file: midpoint -ss + -noaccurate_seek snaps onto the window keyframe;
/// -segment_times / force_key_frames stay relative to that keyframe (not the midpoint).
/// Pad one segment before/after so a previous-IDR snap lands in a discarded file.
/// </summary>
internal static class FfmpegStreamingArgs
{
    public const string SegmentFmp4MovFlags =
        "frag_keyframe+empty_moov+default_base_moof+skip_trailer";

    public static TimeSpan ResolveTransmuxSeekTime(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        bool needsTranscode)
    {
        _ = needsTranscode;
        var startTime = ResolveTimelineOrigin(allSegments, startSegmentIndex);
        if (startSegmentIndex <= 0)
            return startTime;

        // Exact keyframe -ss often snaps to the PREVIOUS IDR (rewind into prior GOP).
        // Seek halfway into this segment's GOP with -noaccurate_seek so demux lands here.
        var startMs = allSegments[startSegmentIndex].StartTimestamp;
        long nextMs;
        if (startSegmentIndex + 1 < allSegments.Count)
            nextMs = allSegments[startSegmentIndex + 1].StartTimestamp;
        else
            nextMs = allSegments[startSegmentIndex].StartTimestamp + allSegments[startSegmentIndex].Duration;

        return TimeSpan.FromMilliseconds((startMs + nextMs) / 2.0);
    }

    /// <summary>
    /// Pad one segment before/after the deliver window so midpoint -ss + segment_times
    /// cut on the requested keyframes. Padding files are discarded after ffmpeg exits.
    /// </summary>
    public static (int FfmpegStartIndex, int FfmpegEndIndexExclusive) ResolveVideoFfmpegWindow(
        int deliverStartIndex,
        int deliverEndIndexExclusive,
        int segmentCount)
    {
        if (segmentCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(segmentCount));
        if (deliverStartIndex < 0 || deliverEndIndexExclusive > segmentCount
            || deliverStartIndex >= deliverEndIndexExclusive)
        {
            throw new ArgumentException("Invalid deliver segment range");
        }

        var ffmpegStart = deliverStartIndex > 0 ? deliverStartIndex - 1 : 0;
        var ffmpegEnd = deliverEndIndexExclusive < segmentCount
            ? deliverEndIndexExclusive + 1
            : deliverEndIndexExclusive;
        return (ffmpegStart, ffmpegEnd);
    }

    public static TimeSpan ResolveTimelineOrigin(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex) =>
        TimeSpan.FromMilliseconds(allSegments[startSegmentIndex].StartTimestamp);

    /// <summary>
    /// Absolute demux end time for input-side -to. When -ss seeks past the segment
    /// keyframe (midpoint), extend -to by the same pad so the window duration stays correct.
    /// </summary>
    public static TimeSpan ResolveInputEndTime(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan seekTime)
    {
        TimeSpan endRef;
        if (endSegmentIndex < allSegments.Count)
        {
            endRef = TimeSpan.FromMilliseconds(allSegments[endSegmentIndex].StartTimestamp);
        }
        else
        {
            var last = allSegments[endSegmentIndex - 1];
            endRef = TimeSpan.FromMilliseconds(last.StartTimestamp + last.Duration);
        }

        var segmentStart = ResolveTimelineOrigin(allSegments, startSegmentIndex);
        if (seekTime > TimeSpan.Zero && seekTime > segmentStart)
            endRef += seekTime - segmentStart;

        return endRef;
    }

    public static List<double> BuildRelativeSplitTimes(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan timelineOrigin)
    {
        // Cuts follow the post-seek output timeline. With -noaccurate_seek that origin
        // is the keyframe (ResolveTimelineOrigin), NOT the midpoint -ss value.
        var splits = new List<double>();
        var originSeconds = timelineOrigin.TotalSeconds;
        for (var i = startSegmentIndex + 1; i < endSegmentIndex && i < allSegments.Count; i++)
        {
            var absoluteSeconds = allSegments[i].StartTimestamp / 1000.0;
            var relative = absoluteSeconds - originSeconds;
            if (relative > 0.001)
                splits.Add(relative);
        }

        return splits;
    }

    /// <summary>
    /// Input-side args that must appear before -i (seek + demux end).
    /// </summary>
    public static IReadOnlyList<string> BuildKeyframeAlignedInputArguments(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan seekTime,
        bool copyAudio,
        bool noAccurateSeek = false)
    {
        var args = new List<string>();

        if (seekTime > TimeSpan.Zero)
        {
            if (noAccurateSeek && startSegmentIndex > 0)
                args.Add("-noaccurate_seek");

            args.Add($"-ss {seekTime.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)}");
        }

        // Audio copy uses output -t; video/audio encode limit the demux window with input -to.
        if (!copyAudio)
        {
            var endRef = ResolveInputEndTime(allSegments, startSegmentIndex, endSegmentIndex, seekTime);
            args.Add($"-to {endRef.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)}");
        }

        args.Add("-fflags +genpts");
        return args;
    }

    /// <summary>
    /// Output-side args for -f segment fMP4 on the shared keyframe timeline (video transmux/encode).
    /// Duration is limited by input -to; do not add output -t (breaks copyts + mid-seek).
    /// </summary>
    public static IReadOnlyList<string> BuildKeyframeAlignedSegmentArguments(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan timelineOrigin,
        TimeSpan endTime)
    {
        _ = endTime;

        var args = new List<string>
        {
            "-copyts",
            "-start_at_zero",
            "-muxdelay 0",
            "-max_muxing_queue_size 2048",
            "-f segment",
            "-segment_time_delta 0.05",
            "-segment_format mp4",
            "-segment_header_filename init.m4s",
            $"-segment_format_options movflags=+{SegmentFmp4MovFlags}",
            $"-segment_start_number {startSegmentIndex}"
        };

        AppendSegmentTimesOrFallback(args, allSegments, startSegmentIndex, endSegmentIndex, timelineOrigin);
        return args;
    }

    /// <summary>
    /// Audio bitstream-copy into demuxed fMP4. No -start_at_zero.
    /// Requires a second output-side -ss (in addition to input -ss) so -t / segment_times
    /// see a coherent timeline; without it mid-seek windows write init + empty N.m4s.
    /// </summary>
    public static IReadOnlyList<string> BuildKeyframeAlignedAudioCopySegmentArguments(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan endTime)
    {
        var timelineOrigin = ResolveTimelineOrigin(allSegments, startSegmentIndex);
        var duration = endTime - timelineOrigin;
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        var args = new List<string>();

        if (timelineOrigin > TimeSpan.Zero)
        {
            args.Add(
                $"-ss {timelineOrigin.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)}");
        }

        args.Add($"-t {duration.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
        args.Add("-copyts");
        args.Add("-copytb 1");
        args.Add("-muxdelay 0");
        args.Add("-max_muxing_queue_size 2048");

        if (timelineOrigin > TimeSpan.Zero)
        {
            args.Add(
                $"-output_ts_offset {timelineOrigin.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)}");
        }

        args.Add("-f segment");
        args.Add("-segment_time_delta 0.05");
        args.Add("-segment_format mp4");
        args.Add("-segment_header_filename init.m4s");
        args.Add($"-segment_format_options movflags=+{SegmentFmp4MovFlags}");
        args.Add($"-segment_start_number {startSegmentIndex}");

        AppendSegmentTimesOrFallback(args, allSegments, startSegmentIndex, endSegmentIndex, timelineOrigin);
        return args;
    }

    private static void AppendSegmentTimesOrFallback(
        List<string> args,
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan timelineOrigin)
    {
        var splitTimes = BuildRelativeSplitTimes(
            allSegments,
            startSegmentIndex,
            endSegmentIndex,
            timelineOrigin);
        if (splitTimes.Count > 0)
        {
            args.Add(
                $"-segment_times {string.Join(",", splitTimes.Select(t => t.ToString("F6", CultureInfo.InvariantCulture)))}");
        }
        else
        {
            args.Add("-segment_time 999999");
        }
    }

    /// <summary>
    /// Encode-side args so IDR frames match -segment_times boundaries.
    /// </summary>
    public static IReadOnlyList<string> BuildKeyframeAlignedEncodeArguments(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan timelineOrigin,
        string logicalCodec,
        string? encoderName)
    {
        var keyframeTimes = new List<string> { "0" };
        foreach (var split in BuildRelativeSplitTimes(
                     allSegments,
                     startSegmentIndex,
                     endSegmentIndex,
                     timelineOrigin))
        {
            keyframeTimes.Add(split.ToString("F6", CultureInfo.InvariantCulture));
        }

        var args = new List<string>
        {
            "-forced-idr 1",
            $"-force_key_frames {string.Join(",", keyframeTimes)}"
        };

        var effectiveEncoder = encoderName
            ?? (logicalCodec is "h264" or "hevc" or "h265"
                ? (logicalCodec == "h264" ? "libx264" : "libx265")
                : null);

        if (effectiveEncoder is not null
            && (effectiveEncoder.Contains("libx264", StringComparison.OrdinalIgnoreCase)
                || effectiveEncoder.Contains("libx265", StringComparison.OrdinalIgnoreCase)))
        {
            args.Add("-bf 0");
        }

        if (logicalCodec is "hevc" or "h265")
            args.Add("-tag:v hvc1");

        return args;
    }
}
