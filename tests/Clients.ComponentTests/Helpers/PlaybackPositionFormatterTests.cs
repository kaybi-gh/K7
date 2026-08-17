using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PlaybackPositionFormatterTests
{
    [Test]
    public void TryFormat_ShouldReturnNull_WhenPositionIsUnderOneSecond()
    {
        PlaybackPositionFormatter.TryFormat(0).Should().BeNull();
        PlaybackPositionFormatter.TryFormat(0.4).Should().BeNull();
        PlaybackPositionFormatter.TryFormat(0.99).Should().BeNull();
    }

    [Test]
    public void TryFormat_ShouldReturnSeconds_WhenPositionIsAtLeastOneSecond()
    {
        PlaybackPositionFormatter.TryFormat(1).Should().Be("1s");
        PlaybackPositionFormatter.TryFormat(45.9).Should().Be("45s");
    }
}
