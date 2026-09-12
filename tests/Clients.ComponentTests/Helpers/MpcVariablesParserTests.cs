using AwesomeAssertions;
using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MpcVariablesParserTests
{
    private const string SampleHtml =
        """
        <html><body>
        <p id="filepath">C:\media\film.mkv</p>
        <p id="state">1</p>
        <p id="statestring">Paused</p>
        <p id="position">4323000</p>
        <p id="duration">7200000</p>
        </body></html>
        """;

    [Test]
    public void TryParse_ShouldReadPositionDurationAndPausedState()
    {
        var parsed = MpcVariablesParser.TryParse(SampleHtml);

        parsed.Should().NotBeNull();
        parsed!.FilePath.Should().Be(@"C:\media\film.mkv");
        parsed.State.Should().Be(PlaybackState.Paused);
        parsed.PositionSeconds.Should().Be(4323);
        parsed.DurationSeconds.Should().Be(7200);
    }

    [Test]
    public void TryParse_ShouldMapPlayingFromStateString()
    {
        var html = """<p id="statestring">Playing</p><p id="position">1000</p><p id="duration">2000</p>""";

        MpcVariablesParser.TryParse(html)!.State.Should().Be(PlaybackState.Playing);
    }

    [Test]
    public void TryParse_ShouldMapStoppedToEnded()
    {
        var html = """<p id="statestring">Stopped</p><p id="state">-1</p><p id="position">0</p><p id="duration">0</p>""";

        MpcVariablesParser.TryParse(html)!.State.Should().Be(PlaybackState.Ended);
    }

    [Test]
    public void TryParse_ShouldReturnNull_WhenHtmlHasNoVariables()
    {
        MpcVariablesParser.TryParse("<html></html>").Should().BeNull();
        MpcVariablesParser.TryParse(null).Should().BeNull();
    }
}
