using K7.Server.Domain.Entities;
using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class FfmpegStreamingArgsTests
{
    [Test]
    public void BuildKeyframeAlignedSegmentArguments_ShouldMatchStreamingContract()
    {
        var segments = BuildSegments(
            (0, 2000),
            (2000, 2000),
            (4000, 2000),
            (6000, 2000));

        var timelineOrigin = TimeSpan.FromSeconds(2);
        var end = TimeSpan.FromSeconds(8);
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedSegmentArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 4,
            timelineOrigin,
            end);

        args.Should().NotContain(a => a.StartsWith("-t ", StringComparison.Ordinal));
        args.Should().Contain("-copyts");
        args.Should().Contain("-start_at_zero");
        args.Should().NotContain("-copytb 1");
        args.Should().NotContain(a => a.StartsWith("-ss ", StringComparison.Ordinal));
        args.Should().NotContain(a => a.StartsWith("-output_ts_offset ", StringComparison.Ordinal));
        args.Should().Contain("-muxdelay 0");
        args.Should().Contain("-f segment");
        args.Should().Contain("-segment_time_delta 0.05");
        args.Should().Contain("-segment_format mp4");
        args.Should().Contain("-segment_header_filename init.m4s");
        args.Should().Contain("-segment_start_number 1");
        args.Should().Contain(
            $"-segment_format_options movflags=+{FfmpegStreamingArgs.SegmentFmp4MovFlags}");
        // Splits relative to keyframe origin (2s), not midpoint -ss.
        args.Should().Contain("-segment_times 2.000000,4.000000");
        args.Should().NotContain(a => a.Contains("frag_discont", StringComparison.Ordinal));
        args.Should().NotContain(a => a.StartsWith("-hls_time", StringComparison.Ordinal));
        args.Should().NotContain(a => a.StartsWith("-to ", StringComparison.Ordinal));
    }

    [Test]
    public void BuildKeyframeAlignedSegmentArguments_ShouldUseKeyframeOrigin_NotMidpointSeek()
    {
        var segments = BuildSegments(
            (0, 2000),
            (2000, 2000),
            (4000, 2000),
            (6000, 2000));

        var timelineOrigin = TimeSpan.FromSeconds(2);
        var end = TimeSpan.FromSeconds(8);
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedSegmentArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 4,
            timelineOrigin,
            end);

        args.Should().Contain("-segment_times 2.000000,4.000000");
        args.Should().NotContain("-segment_times 1.000000,3.000000");
    }

    [Test]
    public void BuildKeyframeAlignedSegmentArguments_ShouldSubtractEncoderDelayFromTsOffset()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000), (4000, 2000));
        var delay = TimeSpan.FromSeconds(1024 / 48000.0);
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedSegmentArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 3,
            timelineOrigin: TimeSpan.FromSeconds(2),
            endTime: TimeSpan.FromSeconds(6),
            encoderDelay: delay,
            resetTimelineToZero: false);

        var expectedOffset = (TimeSpan.FromSeconds(2) - delay).TotalSeconds
            .ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        args.Should().Contain("-ss 2.000000");
        args.Should().Contain("-output_ts_offset " + expectedOffset);
        args.Should().NotContain("-output_ts_offset 2.000000");
        args.Should().NotContain("-start_at_zero");
    }

    [Test]
    public void BuildKeyframeAlignedInputArguments_ShouldUseNoAccurateSeek_WhenRequested()
    {
        var segments = BuildSegments(
            (0, 2000),
            (2000, 2000),
            (4000, 2000),
            (6000, 2000));

        var seek = TimeSpan.FromSeconds(3);
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedInputArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 4,
            seek,
            copyAudio: false,
            noAccurateSeek: true);

        args.Should().Contain("-ss 3.000000");
        args.Should().Contain("-noaccurate_seek");
        // Window reaches EOF (end == count): -to at media end. No midpoint seek pad.
        args.Should().Contain("-to 8.000000");
        args.Should().Contain("-fflags +genpts");
    }

    [Test]
    public void BuildKeyframeAlignedInputArguments_ShouldOmitNoAccurateSeek_ForExactAudioSeek()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000), (4000, 2000));
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedInputArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 3,
            seekTime: TimeSpan.FromSeconds(2),
            copyAudio: false,
            noAccurateSeek: false);

        args.Should().Contain("-ss 2.000000");
        args.Should().NotContain("-noaccurate_seek");
        args.Should().Contain("-to 6.000000");
    }

    [Test]
    public void BuildKeyframeAlignedInputArguments_ShouldOmitTo_WhenAudioCopy()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000));
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedInputArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 2,
            seekTime: TimeSpan.FromSeconds(2),
            copyAudio: true);

        args.Should().Contain("-ss 2.000000");
        args.Should().NotContain(a => a.StartsWith("-to ", StringComparison.Ordinal));
        args.Should().NotContain("-noaccurate_seek");
    }

    [Test]
    public void ResolveInputEndTime_ShouldDemuxPastCloserKeyframe_WithoutSeekPad()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000), (4000, 2000), (6000, 2000));
        var end = FfmpegStreamingArgs.ResolveInputEndTime(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 3,
            seekTime: TimeSpan.FromSeconds(3));

        // Exclusive end is segment 3 (6s). Demux through it to segment 4 start (8s) so
        // -segment_times can close the last window file. Midpoint seek pad is not added:
        // remux lands on the keyframe via -noaccurate_seek.
        end.TotalSeconds.Should().Be(8);
    }

    [Test]
    public void ResolveInputEndTime_ShouldUseMediaEnd_WhenWindowReachesEof()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000), (4000, 2000), (6000, 2000));
        var end = FfmpegStreamingArgs.ResolveInputEndTime(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 4,
            seekTime: TimeSpan.FromSeconds(3));

        end.TotalSeconds.Should().Be(8);
    }

    [Test]
    public void BuildRelativeSplitTimes_ShouldIncludeCloserCut_WhenWindowEndsBeforeEof()
    {
        var segments = BuildSegments(
            (0, 2000),
            (2000, 2000),
            (4000, 2000),
            (6000, 2000),
            (8000, 2000));

        var splits = FfmpegStreamingArgs.BuildRelativeSplitTimes(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 4,
            timelineOrigin: TimeSpan.FromSeconds(2));

        // Interior cuts at 2s/4s relative, plus closer at exclusive end (6s relative).
        splits.Should().Equal(2.0, 4.0, 6.0);
    }

    [Test]
    public void ResolveCloserSegmentIndex_ShouldReturnExclusiveEnd_WhenBeforeEof()
    {
        FfmpegStreamingArgs.ResolveCloserSegmentIndex(4, 10).Should().Be(4);
        FfmpegStreamingArgs.ResolveCloserSegmentIndex(10, 10).Should().BeNull();
        FfmpegStreamingArgs.ResolveCloserSegmentIndex(-1, 10).Should().BeNull();
    }

    [Test]
    public void BuildKeyframeAlignedAudioCopySegmentArguments_ShouldMatchAudioCopyContract()
    {
        var segments = BuildSegments(
            (0, 2000),
            (2000, 2000),
            (4000, 2000),
            (6000, 2000));

        var args = FfmpegStreamingArgs.BuildKeyframeAlignedAudioCopySegmentArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 4,
            endTime: TimeSpan.FromSeconds(8));

        args.Should().Contain("-t 6.000");
        args.Should().Contain("-ss 2.000000");
        args.Should().Contain("-copyts");
        args.Should().Contain("-copytb 1");
        args.Should().Contain("-muxdelay 0");
        args.Should().Contain("-output_ts_offset 2.000000");
        args.Should().Contain("-segment_times 2.000000,4.000000");
        args.Should().NotContain("-start_at_zero");
        args.Should().NotContain(a => a.StartsWith("-avoid_negative_ts", StringComparison.Ordinal));
        var ssIndex = Array.FindIndex(args.ToArray(), a => a.StartsWith("-ss ", StringComparison.Ordinal));
        var tIndex = Array.FindIndex(args.ToArray(), a => a.StartsWith("-t ", StringComparison.Ordinal));
        ssIndex.Should().BeGreaterThanOrEqualTo(0);
        tIndex.Should().BeGreaterThan(ssIndex);
    }

    [Test]
    public void BuildKeyframeAlignedSegmentArguments_ShouldUseSegmentTime_WhenSingleSegmentWindow()
    {
        var segments = BuildSegments((0, 2000));
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedSegmentArguments(
            segments,
            startSegmentIndex: 0,
            endSegmentIndex: 1,
            timelineOrigin: TimeSpan.Zero,
            endTime: TimeSpan.FromSeconds(2));

        args.Should().Contain("-segment_time 999999");
        args.Should().NotContain(a => a.StartsWith("-segment_times ", StringComparison.Ordinal));
    }

    [Test]
    public void BuildKeyframeAlignedEncodeArguments_ShouldForceIdrOnSameSplits()
    {
        var segments = BuildSegments(
            (0, 2000),
            (2000, 2000),
            (4000, 2000));

        var args = FfmpegStreamingArgs.BuildKeyframeAlignedEncodeArguments(
            segments,
            startSegmentIndex: 0,
            endSegmentIndex: 3,
            timelineOrigin: TimeSpan.Zero,
            logicalCodec: "h264",
            encoderName: "libx264");

        args.Should().Contain("-forced-idr 1");
        args.Should().Contain("-force_key_frames source");
        args.Should().Contain("-sc_threshold 0");
        args.Should().Contain("-bf 0");
        args.Should().Contain("-strict -2");
        args.Should().NotContain("-tag:v hvc1");
    }

    [Test]
    public void ResolveVideoCutOrigin_ShouldUseKeyframe_WhenEncode()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000), (4000, 2000));
        var seek = TimeSpan.FromSeconds(3);
        var origin = FfmpegStreamingArgs.ResolveVideoCutOrigin(
            segments,
            startSegmentIndex: 1,
            seek,
            isEncode: true);

        origin.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void ResolveVideoCutOrigin_ShouldUseKeyframe_WhenRemux()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000), (4000, 2000));
        var origin = FfmpegStreamingArgs.ResolveVideoCutOrigin(
            segments,
            startSegmentIndex: 1,
            seekTime: TimeSpan.FromSeconds(3),
            isEncode: false);

        origin.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void BuildKeyframeAlignedEncodeArguments_ShouldForceSourceKeyframes()
    {
        var segments = BuildSegments(
            (0, 2000),
            (2000, 2000),
            (4000, 2000),
            (6000, 2000));

        var args = FfmpegStreamingArgs.BuildKeyframeAlignedEncodeArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 4,
            timelineOrigin: TimeSpan.FromSeconds(2),
            logicalCodec: "h264",
            encoderName: "h264_nvenc");

        args.Should().Contain("-force_key_frames source");
        args.Should().NotContain(a => a.StartsWith("-force_key_frames 2.", StringComparison.Ordinal));
        var segmentArgs = FfmpegStreamingArgs.BuildKeyframeAlignedSegmentArguments(
            segments,
            startSegmentIndex: 1,
            endSegmentIndex: 4,
            timelineOrigin: TimeSpan.FromSeconds(2),
            endTime: TimeSpan.FromSeconds(8));
        segmentArgs.Should().Contain("-segment_times 2.000000,4.000000");
    }

    [Test]
    public void BuildKeyframeAlignedEncodeArguments_ShouldDisableBFrames_WhenHardwareEncoder()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000));
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedEncodeArguments(
            segments,
            startSegmentIndex: 0,
            endSegmentIndex: 2,
            timelineOrigin: TimeSpan.Zero,
            logicalCodec: "h264",
            encoderName: "h264_nvenc");

        args.Should().Contain("-bf 0");
        args.Should().Contain("-g 72");
        args.Should().Contain("-forced-idr 1");
        args.Should().NotContain("-sc_threshold 0");
        args.Should().Contain("-no-scenecut 1");
        args.Should().Contain("-force_key_frames source");
    }

    [Test]
    public void BuildKeyframeAlignedEncodeArguments_ShouldSkipLibx264PrivateOptions_WhenAmf()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000));
        var args = FfmpegStreamingArgs.BuildKeyframeAlignedEncodeArguments(
            segments,
            startSegmentIndex: 0,
            endSegmentIndex: 2,
            timelineOrigin: TimeSpan.Zero,
            logicalCodec: "h264",
            encoderName: "h264_amf");

        args.Should().Contain("-bf 0");
        args.Should().Contain("-g 72");
        args.Should().Contain("-force_key_frames source");
        args.Should().NotContain("-sc_threshold 0");
        args.Should().NotContain("-forced-idr 1");
        args.Should().NotContain("-no-scenecut 1");
    }

    [Test]
    public void ResolveTransmuxSeekTime_ShouldSeekPastKeyframe_WhenRemuxMidFile()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000), (4000, 2000));
        var seek = FfmpegStreamingArgs.ResolveTransmuxSeekTime(segments, startSegmentIndex: 1, needsTranscode: false);
        // RemuxSeekClearanceMs (250) - 50ms pad past the playlist IDR.
        seek.TotalMilliseconds.Should().Be(2200);
    }

    [Test]
    public void ResolveTransmuxSeekTime_ShouldKeepPadInsideShortSegment_WhenRemux()
    {
        var segments = BuildSegments((0, 2000), (2000, 100), (2100, 2000));
        var seek = FfmpegStreamingArgs.ResolveTransmuxSeekTime(segments, startSegmentIndex: 1, needsTranscode: false);
        seek.TotalMilliseconds.Should().Be(2075);
    }

    [Test]
    public void ResolveTransmuxSeekTime_ShouldUseExactKeyframe_WhenEncodeMidFile()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000), (4000, 2000));
        var seek = FfmpegStreamingArgs.ResolveTransmuxSeekTime(segments, startSegmentIndex: 1, needsTranscode: true);
        seek.TotalMilliseconds.Should().Be(2000);
    }

    [Test]
    public void ResolveTransmuxSeekTime_ShouldUseKeyframe_WhenStartingAtZero()
    {
        var segments = BuildSegments((0, 2000), (2000, 2000));
        var seek = FfmpegStreamingArgs.ResolveTransmuxSeekTime(segments, startSegmentIndex: 0, needsTranscode: true);
        seek.TotalMilliseconds.Should().Be(0);
    }

    [Test]
    public void NormalizeOutputDirectory_ShouldReturnAbsolutePath_WhenTranscodingPathIsRelative()
    {
        var relative = Path.Combine("transcoding", "fdf6e46fd6484ce69b32593d07b67d66", "video-original-copy");
        var normalized = FfmpegStreamingArgs.NormalizeOutputDirectory(relative);

        Path.IsPathFullyQualified(normalized).Should().BeTrue();
        Path.Combine(normalized, "%d.m4s").Should().StartWith(normalized);
        normalized.Should().NotContain(Path.Combine(relative, relative));
    }

    [Test]
    public void ResolveVideoFfmpegWindow_ShouldPadStartAndEnd_WhenMidFile()
    {
        FfmpegStreamingArgs.ResolveVideoFfmpegWindow(5, 10, 20).Should().Be((4, 11));
        FfmpegStreamingArgs.ResolveVideoFfmpegWindow(0, 5, 10).Should().Be((0, 6));
        FfmpegStreamingArgs.ResolveVideoFfmpegWindow(5, 10, 10).Should().Be((4, 10));
        FfmpegStreamingArgs.ResolveVideoFfmpegWindow(0, 10, 10).Should().Be((0, 10));
        FfmpegStreamingArgs.ResolveVideoFfmpegWindow(5, 10, 20, padBefore: false).Should().Be((5, 11));
    }

    [Test]
    public void BuildVideoFilterChain_ShouldPutTonemapBeforeScaleAndHwupload()
    {
        var tonemap = FfmpegVideoEncoderBuilder.GetHdrTonemapFilter(true);
        var chain = FfmpegVideoEncoderBuilder.BuildVideoFilterChain(tonemap, 720, "format=nv12,hwupload");

        chain.Should().StartWith("zscale=transfer=linear:npl=100");
        chain.Should().Contain(",scale=-2:720,format=nv12,hwupload");
    }

    private static List<HlsSegment> BuildSegments(params (long Start, long Duration)[] rows)
    {
        return rows.Select((row, i) => new HlsSegment
        {
            Number = i,
            StartTimestamp = row.Start,
            Duration = row.Duration
        }).ToList();
    }
}
