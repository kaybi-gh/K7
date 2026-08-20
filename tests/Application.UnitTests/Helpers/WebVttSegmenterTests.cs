using K7.Server.Application.Helpers;

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
}
