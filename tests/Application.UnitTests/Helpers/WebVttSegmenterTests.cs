using K7.Server.Application.Helpers;
using K7.Shared.Constants;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class WebVttSegmenterTests
{
    [Test]
    public void EmptySegment_ShouldReturnValidWebVttHeader()
    {
        var vtt = WebVttSegmenter.EmptySegment();

        vtt.Should().StartWith("WEBVTT");
        vtt.Should().Contain("X-TIMESTAMP-MAP=MPEGTS:0,LOCAL:00:00:00.000");
        vtt.Should().NotContain("-->");
    }

    [Test]
    public void ExtractSegment_ShouldKeepOverlappingCuesOnly()
    {
        const string full = """
            WEBVTT

            00:00:10.000 --> 00:00:12.000
            early

            00:00:29.000 --> 00:00:31.000
            overlap

            00:00:40.000 --> 00:00:42.000
            late
            """;

        var segment = WebVttSegmenter.ExtractSegment(full, startTimeSeconds: 30, endTimeSeconds: 35);

        segment.Should().Contain("00:00:29.000 --> 00:00:31.000");
        segment.Should().Contain("overlap");
        segment.Should().NotContain("early");
        segment.Should().NotContain("late");
    }

    [Test]
    public void MergeWindows_ShouldFlagResultAsPartial()
    {
        var merged = WebVttSegmenter.MergeWindows(["WEBVTT\n\n"], firstWindowStartSeconds: 0, windowSeconds: 120);

        merged.Should().StartWith("WEBVTT");
        SubtitleVttFormat.IsPartial(merged).Should().BeTrue();
        merged.Should().NotContain("-->");
    }

    [Test]
    public void MergeWindows_ShouldWriteEachCueOnce_WhenWindowsOverlap()
    {
        // Window 1 (120-240 s) was extracted from 90 s: it repeats the end of window 0.
        const string window0 = """
            WEBVTT

            01:00.000 --> 01:02.000
            first

            01:55.000 --> 02:03.000
            across the boundary
            """;
        const string window1 = """
            WEBVTT

            01:55.000 --> 02:03.000
            across the boundary

            02:10.000 --> 02:12.000
            second
            """;

        var merged = WebVttSegmenter.MergeWindows([window0, window1], firstWindowStartSeconds: 0, windowSeconds: 120);

        CountOccurrences(merged, "across the boundary").Should().Be(1);
        merged.Should().Contain("first");
        merged.Should().Contain("second");
        merged.IndexOf("first", StringComparison.Ordinal)
            .Should().BeLessThan(merged.IndexOf("second", StringComparison.Ordinal));
    }

    [Test]
    public void MergeWindows_ShouldKeepCuesBeforeTheFirstWindow_AndLeaveTimingsUntouched()
    {
        // Playback is in window 1: the cue that began in window 0 is still on screen at 120 s.
        const string window1 = """
            WEBVTT

            01:55.000 --> 02:03.000
            still on screen

            02:10.500 --> 02:12.250
            next
            """;

        var merged = WebVttSegmenter.MergeWindows([window1], firstWindowStartSeconds: 120, windowSeconds: 120);

        merged.Should().Contain("01:55.000 --> 02:03.000");
        merged.Should().Contain("02:10.500 --> 02:12.250");
    }

    private static int CountOccurrences(string text, string value) =>
        text.Split(value).Length - 1;
}
