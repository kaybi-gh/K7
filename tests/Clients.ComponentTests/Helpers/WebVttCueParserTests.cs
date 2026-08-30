using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class WebVttCueParserTests
{
    [Test]
    public void Parse_ShouldReturnEmpty_WhenVttIsMissing()
    {
        WebVttCueParser.Parse(null).Should().BeEmpty();
        WebVttCueParser.Parse("").Should().BeEmpty();
        WebVttCueParser.Parse("WEBVTT\n\n").Should().BeEmpty();
    }

    [Test]
    public void Parse_ShouldReadCuesAndStripMarkup()
    {
        const string vtt = """
            WEBVTT

            1
            00:00:01.000 --> 00:00:04.000
            <c.white>Hello</c>

            00:01:05.500 --> 00:01:08.000 align:start
            Line one
            Line two
            """;

        var cues = WebVttCueParser.Parse(vtt);

        cues.Should().HaveCount(2);
        cues[0].StartSeconds.Should().BeApproximately(1, 0.001);
        cues[0].EndSeconds.Should().BeApproximately(4, 0.001);
        cues[0].Text.Should().Be("Hello");
        cues[1].StartSeconds.Should().BeApproximately(65.5, 0.001);
        cues[1].Text.Should().Be("Line one\nLine two");
    }

    [Test]
    public void CueAt_ShouldReturnActiveCue()
    {
        var cues = WebVttCueParser.Parse("""
            WEBVTT

            00:00:01.000 --> 00:00:04.000
            First

            00:00:04.000 --> 00:00:08.000
            Second
            """);

        WebVttCueParser.CueAt(cues, 0.5).Should().BeNull();
        WebVttCueParser.CueAt(cues, 1.0).Should().Be("First");
        WebVttCueParser.CueAt(cues, 3.9).Should().Be("First");
        WebVttCueParser.CueAt(cues, 4.0).Should().Be("Second");
        WebVttCueParser.CueAt(cues, 8.0).Should().BeNull();
    }
}
