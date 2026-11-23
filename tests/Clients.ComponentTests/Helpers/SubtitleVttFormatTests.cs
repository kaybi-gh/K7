using K7.Clients.Shared.Helpers;
using K7.Shared.Constants;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SubtitleVttFormatTests
{
    private const string PartialVtt =
        "WEBVTT\r\n\r\n" + SubtitleVttFormat.PartialNote + "\r\n\r\n00:01.000 --> 00:02.000\r\nHi\r\n";

    [Test]
    public void IsPartial_ShouldBeTrue_WhenTheNoteFollowsTheHeader()
    {
        SubtitleVttFormat.IsPartial(PartialVtt).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("WEBVTT\n\n00:01.000 --> 00:02.000\nHi\n")]
    public void IsPartial_ShouldBeFalse_ForACompleteOrEmptyVtt(string? vtt)
    {
        SubtitleVttFormat.IsPartial(vtt).Should().BeFalse();
    }

    [Test]
    public void IsPartial_ShouldIgnoreTheMarkerInsideACue()
    {
        var filler = string.Concat(Enumerable.Repeat("00:01.000 --> 00:02.000\nHi\n\n", 4));
        var vtt = "WEBVTT\n\n" + filler + "00:05.000 --> 00:06.000\n" + SubtitleVttFormat.PartialNote + "\n";

        SubtitleVttFormat.IsPartial(vtt).Should().BeFalse();
    }

    [Test]
    public void PartialNote_ShouldNotBecomeACue_ForTheNativeParser()
    {
        var cues = WebVttCueParser.Parse(PartialVtt);

        cues.Should().ContainSingle().Which.Text.Should().Be("Hi");
    }
}
