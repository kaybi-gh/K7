using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class RemuxHeadOverlapStopPolicyTests
{
    [Test]
    public void ShouldStop_ShouldBeFalse_WhenInitMissingAndLandingAlreadyCached()
    {
        RemuxHeadOverlapStopPolicy.ShouldStopBecauseNextIsReady(
                from: 1403,
                tipIndex: 1403,
                untilInclusive: 1408,
                landingReadyOnShared: true,
                nextReadyOnShared: true,
                nextExistsInStaging: false,
                initReadyOnShared: false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldStop_ShouldBeTrue_WhenInitReadyAndNextAlreadyCached()
    {
        RemuxHeadOverlapStopPolicy.ShouldStopBecauseNextIsReady(
                from: 1403,
                tipIndex: 1403,
                untilInclusive: 1408,
                landingReadyOnShared: true,
                nextReadyOnShared: true,
                nextExistsInStaging: false,
                initReadyOnShared: true)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldStop_ShouldBeFalse_WhenLandingHoleAndNextReady()
    {
        RemuxHeadOverlapStopPolicy.ShouldStopBecauseNextIsReady(
                from: 1403,
                tipIndex: 1403,
                untilInclusive: 1408,
                landingReadyOnShared: false,
                nextReadyOnShared: true,
                nextExistsInStaging: false,
                initReadyOnShared: true)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldStop_ShouldBeFalse_WhenNextStillInStaging()
    {
        RemuxHeadOverlapStopPolicy.ShouldStopBecauseNextIsReady(
                from: 1403,
                tipIndex: 1403,
                untilInclusive: 1408,
                landingReadyOnShared: true,
                nextReadyOnShared: true,
                nextExistsInStaging: true,
                initReadyOnShared: true)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldStop_ShouldBeFalse_WhenTipHasNotMovedPastFrom()
    {
        RemuxHeadOverlapStopPolicy.ShouldStopBecauseNextIsReady(
                from: 1403,
                tipIndex: 1402,
                untilInclusive: 1408,
                landingReadyOnShared: true,
                nextReadyOnShared: true,
                nextExistsInStaging: false,
                initReadyOnShared: true)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldStop_ShouldBeFalse_WhenNextIsPastUntil()
    {
        RemuxHeadOverlapStopPolicy.ShouldStopBecauseNextIsReady(
                from: 1403,
                tipIndex: 1408,
                untilInclusive: 1408,
                landingReadyOnShared: true,
                nextReadyOnShared: true,
                nextExistsInStaging: false,
                initReadyOnShared: true)
            .Should().BeFalse();
    }
}
