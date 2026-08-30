using System.Globalization;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;

namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Pure ffmpeg argument contracts for keyframe-aligned streaming (no process launch).
/// Remux mid-file: short pad past the playlist IDR + -noaccurate_seek lands on that IDR
/// (not a collapsed interior keyframe, and not the previous GOP).
/// Encode: exact keyframe -ss (accurate) - re-encode does not need the remux snap.
/// -segment_times and force_key_frames are relative to that keyframe. -start_at_zero
/// zeroes PTS from the landed IDR, so absolute source times never match the encoder.
/// Pad one segment before/after so a previous-IDR snap lands in a discarded file.
/// Include a closer -segment_times cut at the exclusive window end so the last file
/// does not absorb the next GOP; delete that throwaway .m4s after ffmpeg exits.
/// Video windows use -start_at_zero; audio copy keeps source PTS via output -ss +
/// -output_ts_offset. Serve-side video tfdt rebase maps fragments onto the playlist.
/// Window pads use playlist numbers: restore a previously-ready pad. Keep a new after
/// pad (it is the next playlist index). Delete only a new before pad (seek snap).
/// </summary>
internal static class FfmpegStreamingArgs
{
    public const string SegmentFmp4MovFlags =
        "frag_keyframe+empty_moov+default_base_moof+skip_trailer";

    /// <summary>
    /// ffmpeg WorkingDirectory is the output folder and -segment_header_filename is
    /// relative (init.m4s). The %d.m4s pattern must be absolute: a relative
    /// Paths:Transcoding value would otherwise nest (ENOENT on header write).
    /// </summary>
    public static string NormalizeOutputDirectory(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        return Path.GetFullPath(outputDirectory);
    }

    public static TimeSpan ResolveTransmuxSeekTime(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        bool needsTranscode)
    {
        var startTime = ResolveTimelineOrigin(allSegments, startSegmentIndex);
        if (startSegmentIndex <= 0)
            return startTime;

        // Encode: seek exactly to the keyframe. Accurate decode is fine when re-encoding.
        // Remux sequential continue also uses the past-IDR path below: accurate remux
        // -ss snaps to the previous IDR and writes that GOP as the first new .m4s.
        if (needsTranscode)
            return startTime;

        // Remux: exact keyframe -ss often snaps to the PREVIOUS IDR (rewind into prior GOP).
        // Seek a short pad past the playlist IDR with -noaccurate_seek so demux lands here.
        // Do NOT use the playlist GOP midpoint: collapsed interior IDRs between playlist
        // starts are still in the bitstream. Landing on one desyncs relative -segment_times.
        // HlsKeyframeSegmentBuilder keeps keyframes inside RemuxSeekClearanceMs as boundaries.
        var segment = allSegments[startSegmentIndex];
        var startMs = segment.StartTimestamp;
        var durationMs = segment.Duration;
        if (durationMs <= 0)
            return startTime;

        long padMs;
        if (durationMs >= Hls.RemuxSeekClearanceMs)
            padMs = Hls.RemuxSeekClearanceMs - 50;
        else
            padMs = Math.Max(1, (durationMs * 3) / 4);

        if (padMs >= durationMs)
            padMs = Math.Max(1, durationMs - 1);

        return TimeSpan.FromMilliseconds(startMs + padMs);
    }

    /// <summary>
    /// Pad one segment before/after the deliver window so past-IDR -ss + segment_times
    /// cut on the requested keyframes. Skip the before-pad on sequential continue
    /// (previous playlist index already ready): overwriting it restarts ffmpeg every
    /// 1-2 segments. Pad files use playlist numbers: restore if they were already ready,
    /// otherwise delete the new before-pad so it cannot fake a hole as "ready".
    /// </summary>
    public static (int FfmpegStartIndex, int FfmpegEndIndexExclusive) ResolveVideoFfmpegWindow(
        int deliverStartIndex,
        int deliverEndIndexExclusive,
        int segmentCount,
        bool padBefore = true)
    {
        if (segmentCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(segmentCount));
        if (deliverStartIndex < 0 || deliverEndIndexExclusive > segmentCount
            || deliverStartIndex >= deliverEndIndexExclusive)
        {
            throw new ArgumentException("Invalid deliver segment range");
        }

        var ffmpegStart = padBefore && deliverStartIndex > 0 ? deliverStartIndex - 1 : deliverStartIndex;
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
    /// Origin for -segment_times and relative force_key_frames. Always the segment
    /// keyframe: remux lands there via past-IDR -ss + -noaccurate_seek, encode seeks
    /// there accurately. -start_at_zero zeroes PTS from that frame.
    /// </summary>
    public static TimeSpan ResolveVideoCutOrigin(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        TimeSpan seekTime,
        bool isEncode)
    {
        _ = seekTime;
        _ = isEncode;
        return ResolveTimelineOrigin(allSegments, startSegmentIndex);
    }

    /// <summary>
    /// Absolute demux end time for input-side -to.
    /// Remux lands on the window keyframe via -noaccurate_seek (not midpoint -ss), so do
    /// not extend -to by the seek pad: that packs the next GOP into the last .m4s.
    /// When the window ends before EOF, demux past the exclusive-end keyframe so
    /// -segment_times can close the last playlist file (closer cut).
    /// </summary>
    public static TimeSpan ResolveInputEndTime(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan seekTime)
    {
        _ = startSegmentIndex;
        _ = seekTime;

        if (endSegmentIndex < allSegments.Count)
        {
            // Past the closer keyframe at endSegmentIndex so -f segment can split there.
            if (endSegmentIndex + 1 < allSegments.Count)
            {
                return TimeSpan.FromMilliseconds(
                    allSegments[endSegmentIndex + 1].StartTimestamp);
            }

            var closer = allSegments[endSegmentIndex];
            return TimeSpan.FromMilliseconds(closer.StartTimestamp + closer.Duration);
        }

        var last = allSegments[endSegmentIndex - 1];
        return TimeSpan.FromMilliseconds(last.StartTimestamp + last.Duration);
    }

    public static List<double> BuildRelativeSplitTimes(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan timelineOrigin)
    {
        // Cuts follow the post-seek output timeline at the landed IDR keyframe.
        // Include a cut at endSegmentIndex (when before EOF) so the last window file
        // closes on its playlist boundary instead of absorbing the next GOP.
        var splits = new List<double>();
        var originSeconds = timelineOrigin.TotalSeconds;
        var lastCutExclusive = endSegmentIndex < allSegments.Count
            ? endSegmentIndex + 1
            : endSegmentIndex;
        for (var i = startSegmentIndex + 1; i < lastCutExclusive && i < allSegments.Count; i++)
        {
            var absoluteSeconds = allSegments[i].StartTimestamp / 1000.0;
            var relative = absoluteSeconds - originSeconds;
            if (relative > 0.001)
                splits.Add(relative);
        }

        return splits;
    }

    /// <summary>
    /// Playlist index of the throwaway .m4s opened by the exclusive-end closer cut.
    /// </summary>
    public static int? ResolveCloserSegmentIndex(int endSegmentIndexExclusive, int segmentCount) =>
        endSegmentIndexExclusive >= 0 && endSegmentIndexExclusive < segmentCount
            ? endSegmentIndexExclusive
            : null;

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
    /// Video uses -start_at_zero so landed IDR + relative -segment_times stay coherent;
    /// serve-side tfdt rebase maps fragments onto the playlist. Audio encode keeps source
    /// PTS and subtracts <paramref name="encoderDelay"/> from -output_ts_offset.
    /// </summary>
    public static IReadOnlyList<string> BuildKeyframeAlignedSegmentArguments(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan timelineOrigin,
        TimeSpan endTime,
        TimeSpan encoderDelay = default,
        bool resetTimelineToZero = true)
    {
        var args = new List<string>();
        if (resetTimelineToZero)
        {
            args.Add("-copyts");
            args.Add("-start_at_zero");
            args.Add("-muxdelay 0");
            args.Add("-max_muxing_queue_size 2048");
        }
        else
        {
            AppendSourcePtsOutputTiming(args, timelineOrigin, encoderDelay);
        }

        args.Add("-f segment");
        args.Add(SegmentTimeDeltaArgument);
        args.Add("-segment_format mp4");
        args.Add("-segment_header_filename init.m4s");
        args.Add($"-segment_format_options movflags=+{SegmentFmp4MovFlags}");
        args.Add($"-segment_start_number {startSegmentIndex}");

        AppendSegmentTimesOrFallback(args, allSegments, startSegmentIndex, endSegmentIndex, timelineOrigin);
        return args;
    }

    /// <summary>
    /// Audio bitstream-copy into demuxed fMP4. No -start_at_zero: keep source PTS so A/V
    /// stay in the relationship from the file. Micro-rebasing audio onto the playlist
    /// desyncs every client.
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
        AppendCopytsMuxArgs(args);
        AppendOutputTsOffset(args, timelineOrigin, encoderDelay: TimeSpan.Zero);

        args.Add("-f segment");
        args.Add(SegmentTimeDeltaArgument);
        args.Add("-segment_format mp4");
        args.Add("-segment_header_filename init.m4s");
        args.Add($"-segment_format_options movflags=+{SegmentFmp4MovFlags}");
        args.Add($"-segment_start_number {startSegmentIndex}");

        AppendSegmentTimesOrFallback(args, allSegments, startSegmentIndex, endSegmentIndex, timelineOrigin);
        return args;
    }

    private static void AppendSourcePtsOutputTiming(
        List<string> args,
        TimeSpan timelineOrigin,
        TimeSpan encoderDelay)
    {
        if (timelineOrigin > TimeSpan.Zero)
        {
            args.Add(
                $"-ss {timelineOrigin.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)}");
        }

        AppendCopytsMuxArgs(args);
        AppendOutputTsOffset(args, timelineOrigin, encoderDelay);
    }

    private static void AppendCopytsMuxArgs(List<string> args)
    {
        args.Add("-copyts");
        args.Add("-copytb 1");
        args.Add("-muxdelay 0");
        args.Add("-max_muxing_queue_size 2048");
    }

    private static void AppendOutputTsOffset(
        List<string> args,
        TimeSpan timelineOrigin,
        TimeSpan encoderDelay)
    {
        var offset = timelineOrigin - encoderDelay;
        if (offset == TimeSpan.Zero)
            return;

        args.Add(
            $"-output_ts_offset {offset.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)}");
    }

    private static string SegmentTimeDeltaArgument =>
        $"-segment_time_delta {Hls.SegmentTimeDeltaSeconds.ToString("F2", CultureInfo.InvariantCulture)}";

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
    /// Encode-side args so IDR frames match source keyframes (playlist grid).
    /// force_key_frames source follows input keyframes; relative times miss on AMF/NVENC
    /// and pack 2x GOP into one .m4s. Encode windows seek exactly to the deliver
    /// keyframe (no remux pad), so source keyframes align with -segment_times.
    /// </summary>
    public static IReadOnlyList<string> BuildKeyframeAlignedEncodeArguments(
        IReadOnlyList<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        TimeSpan timelineOrigin,
        string logicalCodec,
        string? encoderName)
    {
        _ = allSegments;
        _ = startSegmentIndex;
        _ = endSegmentIndex;
        _ = timelineOrigin;

        var args = new List<string>
        {
            "-force_key_frames source",
            // Software and hardware HLS encode: B-frames add CTS delay that rebase
            // cannot see, so ExoPlayer treats frames as late and dumps them.
            "-bf 0",
            "-strict -2"
        };

        // libx264 private options; AMF/NVENC/QSV ignore or warn on them.
        if (string.IsNullOrEmpty(encoderName)
            || encoderName.Contains("libx264", StringComparison.OrdinalIgnoreCase)
            || encoderName.Contains("libx265", StringComparison.OrdinalIgnoreCase))
        {
            args.Insert(0, "-forced-idr 1");
            // Scene-cut IDRs land off the playlist. -f segment then cuts at the
            // wrong frame (repeat / skip). Disable scene-cut detection for the same reason.
            args.Insert(1, "-sc_threshold 0");
        }

        if (!string.IsNullOrEmpty(encoderName)
            && encoderName.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("-no-scenecut 1");
        }

        // Hardware encoders default to ~250-frame GOP (~10s at 24fps). Without IDR at
        // each -segment_times cut, -f segment waits for the GOP and packs 2x playlist
        // duration into one .m4s (Web MSE overlap / backwards frames). Cap GOP and
        // force IDR so cuts land on the shared keyframe grid like remux.
        if (IsHardwareEncoder(encoderName))
        {
            args.Insert(0, "-g 72");
            if (encoderName is not null
                && encoderName.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
            {
                args.Insert(1, "-forced-idr 1");
            }
        }

        if (logicalCodec is "hevc" or "h265")
            args.Add("-tag:v hvc1");

        return args;
    }

    private static bool IsHardwareEncoder(string? encoderName) =>
        !string.IsNullOrEmpty(encoderName)
        && (encoderName.Contains("nvenc", StringComparison.OrdinalIgnoreCase)
            || encoderName.Contains("amf", StringComparison.OrdinalIgnoreCase)
            || encoderName.Contains("qsv", StringComparison.OrdinalIgnoreCase)
            || encoderName.Contains("vaapi", StringComparison.OrdinalIgnoreCase));
}
