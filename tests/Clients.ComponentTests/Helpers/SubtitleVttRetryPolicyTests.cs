using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SubtitleVttRetryPolicyTests
{
    [Test]
    public void IsExtractionInProgress_ShouldBeTrue_OnlyFor503()
    {
        SubtitleVttRetryPolicy.IsExtractionInProgress(503).Should().BeTrue();
        SubtitleVttRetryPolicy.IsExtractionInProgress(200).Should().BeFalse();
        SubtitleVttRetryPolicy.IsExtractionInProgress(404).Should().BeFalse();
        SubtitleVttRetryPolicy.IsExtractionInProgress(500).Should().BeFalse();
    }

    [Test]
    public void DelayBeforeRetry_ShouldPollEverySecondFirst_ThenBackOff()
    {
        SubtitleVttRetryPolicy.DelayBeforeRetry(0).Should().Be(TimeSpan.FromSeconds(1));
        SubtitleVttRetryPolicy.DelayBeforeRetry(14).Should().Be(TimeSpan.FromSeconds(1));
        SubtitleVttRetryPolicy.DelayBeforeRetry(15).Should().Be(TimeSpan.FromSeconds(3));
        SubtitleVttRetryPolicy.DelayBeforeRetry(500).Should().Be(TimeSpan.FromSeconds(3));
    }

    [Test]
    public void ShouldGiveUp_ShouldWaitForAFullFileExtraction()
    {
        // The old fixed caps (40 s on Android, 12 s on the XAML sidecar) were shorter than a
        // large remux extraction; the policy must outlast several minutes of 503.
        SubtitleVttRetryPolicy.ShouldGiveUp(TimeSpan.FromSeconds(40)).Should().BeFalse();
        SubtitleVttRetryPolicy.ShouldGiveUp(TimeSpan.FromMinutes(5)).Should().BeFalse();
        SubtitleVttRetryPolicy.ShouldGiveUp(SubtitleVttRetryPolicy.MaxTotalWait).Should().BeTrue();
        SubtitleVttRetryPolicy.ShouldGiveUp(TimeSpan.FromMinutes(11)).Should().BeTrue();
    }

    [Test]
    public void ShouldRefreshPartial_ShouldWaitForTheInterval_WhenPlaybackJustMovesOn()
    {
        // 4 s later at double speed: 8 s further, still the cues of the last answer.
        SubtitleVttRetryPolicy.ShouldRefreshPartial(600, 608, TimeSpan.FromSeconds(4)).Should().BeFalse();
        SubtitleVttRetryPolicy.ShouldRefreshPartial(600, 605, SubtitleVttRetryPolicy.PartialRefreshInterval)
            .Should().BeTrue();
    }

    [TestCase(600, 3600)]
    [TestCase(3600, 600)]
    public void ShouldRefreshPartial_ShouldAskAtOnce_AfterASeek(double requestedPosition, double currentPosition)
    {
        SubtitleVttRetryPolicy.ShouldRefreshPartial(requestedPosition, currentPosition, TimeSpan.FromMilliseconds(500))
            .Should().BeTrue();
    }
}
