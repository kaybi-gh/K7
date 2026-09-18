using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class AndroidAutoPlaybackCommandsTests
{
    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(2, 1)]
    [TestCase(7, 4)]
    [TestCase(10, 5)]
    public void ToStars_ShouldRoundHalfAwayFromZero(int value, int stars)
    {
        AndroidAutoPlaybackCommands.ToStars(value).Should().Be(stars);
    }

    [Test]
    public void CycleValue_ShouldWalkFullStarsThenClear()
    {
        AndroidAutoPlaybackCommands.CycleValue(0).Should().Be(2);
        AndroidAutoPlaybackCommands.CycleValue(2).Should().Be(4);
        AndroidAutoPlaybackCommands.CycleValue(4).Should().Be(6);
        AndroidAutoPlaybackCommands.CycleValue(6).Should().Be(8);
        AndroidAutoPlaybackCommands.CycleValue(8).Should().Be(10);
        AndroidAutoPlaybackCommands.CycleValue(10).Should().Be(0);
    }

    [Test]
    public void CycleValue_ShouldAdvanceFromHalfStarDisplay()
    {
        AndroidAutoPlaybackCommands.CycleValue(7).Should().Be(10);
    }

    [Test]
    public void Repeat_ShouldRoundTripMedia3Values()
    {
        AndroidAutoPlaybackCommands.ToMedia3Repeat(RepeatMode.Off)
            .Should().Be(AndroidAutoPlaybackCommands.RepeatOff);
        AndroidAutoPlaybackCommands.ToMedia3Repeat(RepeatMode.One)
            .Should().Be(AndroidAutoPlaybackCommands.RepeatOne);
        AndroidAutoPlaybackCommands.ToMedia3Repeat(RepeatMode.All)
            .Should().Be(AndroidAutoPlaybackCommands.RepeatAll);

        AndroidAutoPlaybackCommands.FromMedia3Repeat(AndroidAutoPlaybackCommands.RepeatOff)
            .Should().Be(RepeatMode.Off);
        AndroidAutoPlaybackCommands.FromMedia3Repeat(AndroidAutoPlaybackCommands.RepeatOne)
            .Should().Be(RepeatMode.One);
        AndroidAutoPlaybackCommands.FromMedia3Repeat(AndroidAutoPlaybackCommands.RepeatAll)
            .Should().Be(RepeatMode.All);
    }

    [Test]
    public void ResolveValue_ShouldPreferOverlayIncludingZero()
    {
        AndroidAutoPlaybackCommands.ResolveValue(0, 8).Should().Be(0);
        AndroidAutoPlaybackCommands.ResolveValue(6, 8).Should().Be(6);
        AndroidAutoPlaybackCommands.ResolveValue(null, 8).Should().Be(8);
        AndroidAutoPlaybackCommands.ResolveValue(null, null).Should().Be(0);
    }
}
