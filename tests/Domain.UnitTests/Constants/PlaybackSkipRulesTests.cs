using K7.Server.Domain.Constants;

namespace K7.Server.Domain.UnitTests.Constants;

[TestFixture]
public class PlaybackSkipRulesTests
{
    [Test]
    public void IsSkippedListen_ShouldBeTrue_WhenFinishedWithLittleProgress()
    {
        PlaybackSkipRules.IsSkippedListen(isCompleted: false, isFinished: true, watchedSeconds: 8)
            .Should().BeTrue();
    }

    [Test]
    public void IsSkippedListen_ShouldBeFalse_WhenCompleted()
    {
        PlaybackSkipRules.IsSkippedListen(isCompleted: true, isFinished: true, watchedSeconds: 5)
            .Should().BeFalse();
    }

    [Test]
    public void IsSkippedListen_ShouldBeFalse_WhenMeaningfulProgress()
    {
        PlaybackSkipRules.IsSkippedListen(isCompleted: false, isFinished: true, watchedSeconds: 90)
            .Should().BeFalse();
    }

    [Test]
    public void IsSkippedListen_ShouldBeFalse_WhenStillOpen()
    {
        PlaybackSkipRules.IsSkippedListen(isCompleted: false, isFinished: false, watchedSeconds: 0)
            .Should().BeFalse();
    }
}
