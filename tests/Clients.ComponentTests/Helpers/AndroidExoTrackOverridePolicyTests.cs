using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class AndroidExoTrackOverridePolicyTests
{
    private const string EpisodeOne = "https://k7.local/stream/ep1/master.m3u8?session=1";
    private const string EpisodeTwo = "https://k7.local/stream/ep2/master.m3u8?session=2";

    [Test]
    public void ShouldApplyTrackOverrides_ShouldBeFalse_WhenSignatureEmpty()
    {
        AndroidExoTrackOverridePolicy
            .ShouldApplyTrackOverrides(EpisodeTwo, "old-groups", EpisodeTwo, currentSignature: null)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldApplyTrackOverrides_ShouldBeFalse_WhenSameUrlAndSignature()
    {
        AndroidExoTrackOverridePolicy
            .ShouldApplyTrackOverrides(EpisodeTwo, "groups-b", EpisodeTwo, "groups-b")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldApplyTrackOverrides_ShouldBeTrue_WhenTrackGroupsChanged()
    {
        AndroidExoTrackOverridePolicy
            .ShouldApplyTrackOverrides(EpisodeTwo, "groups-a", EpisodeTwo, "groups-b")
            .Should().BeTrue();
    }

    [Test]
    public void ShouldApplyTrackOverrides_ShouldBeTrue_WhenUrlChanged()
    {
        AndroidExoTrackOverridePolicy
            .ShouldApplyTrackOverrides(EpisodeOne, "groups-a", EpisodeTwo, "groups-a")
            .Should().BeTrue();
    }

    [Test]
    public void MediaItemMatchesSource_ShouldBeTrue_WhenRequestUrlMatches()
    {
        AndroidExoTrackOverridePolicy
            .MediaItemMatchesSource(EpisodeTwo, EpisodeTwo)
            .Should().BeTrue();
    }

    [Test]
    public void MediaItemMatchesSource_ShouldBeTrue_WhenOnlyEscapingDiffers()
    {
        AndroidExoTrackOverridePolicy
            .MediaItemMatchesSource(
                "https://k7.local/stream/my%20show/master.m3u8",
                "https://k7.local/stream/my show/master.m3u8")
            .Should().BeTrue();
    }

    [Test]
    public void MediaItemMatchesSource_ShouldBeFalse_WhenEpisodeUrlDiffers()
    {
        AndroidExoTrackOverridePolicy
            .MediaItemMatchesSource(EpisodeOne, EpisodeTwo)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldRememberTrackOverride_ShouldBeFalse_WhenSelectionStillPending()
    {
        AndroidExoTrackOverridePolicy
            .ShouldRememberTrackOverride(AndroidExoTrackOverridePolicy.SubtitleBind.Pending)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldRememberTrackOverride_ShouldBeTrue_WhenSelectionAppliedOrDeferred()
    {
        AndroidExoTrackOverridePolicy
            .ShouldRememberTrackOverride(AndroidExoTrackOverridePolicy.SubtitleBind.Applied)
            .Should().BeTrue();
        AndroidExoTrackOverridePolicy
            .ShouldRememberTrackOverride(AndroidExoTrackOverridePolicy.SubtitleBind.Deferred)
            .Should().BeTrue();
        AndroidExoTrackOverridePolicy
            .ShouldRememberTrackOverride(AndroidExoTrackOverridePolicy.SubtitleBind.Cleared)
            .Should().BeTrue();
    }
}
