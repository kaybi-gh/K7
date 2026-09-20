using AwesomeAssertions;
using K7.Clients.MAUI.Playback;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public sealed class VlcTimeMapDemuxTests
{
    [Test]
    public void MapDemuxSeconds_ShouldStayRealtime_WhenPositionIsRelativeAfterStartTime()
    {
        bool? relative = null;
        const double epoch = 3600;
        const double duration = 7200;

        // Mid remaining span: absolute heuristic would wrongly return Position*duration (fast).
        var mid = VlcTime.MapDemuxSeconds(0, 0.5f, duration, epoch, ref relative);
        mid.Should().BeApproximately(5400, 0.01);
        relative.Should().BeTrue();

        var later = VlcTime.MapDemuxSeconds(0, 0.75f, duration, epoch, ref relative);
        later.Should().BeApproximately(6300, 0.01);
        relative.Should().BeTrue();
    }

    [Test]
    public void MapDemuxSeconds_ShouldNotFlipToAbsolute_WhenRelativePositionCrossesEpochRatio()
    {
        bool? relative = true;
        const double epoch = 60;
        const double duration = 7200;

        var after = VlcTime.MapDemuxSeconds(0, 0.02f, duration, epoch, ref relative);
        after.Should().BeApproximately(60 + 0.02 * (duration - epoch), 0.05);
        relative.Should().BeTrue();
    }

    [Test]
    public void MapDemuxSeconds_ShouldPreferAbsoluteTime_WhenDemuxCatchesUp()
    {
        bool? relative = true;
        const double epoch = 3600;

        var mapped = VlcTime.MapDemuxSeconds(3605, 0.1f, 7200, epoch, ref relative);
        mapped.Should().BeApproximately(3605, 0.01);
        relative.Should().BeFalse();
    }

    [Test]
    public void MapDemuxSeconds_ShouldAddEpoch_WhenTimeRestartsNearZero()
    {
        bool? relative = null;
        const double epoch = 3600;

        var mapped = VlcTime.MapDemuxSeconds(12, 0, 7200, epoch, ref relative);
        mapped.Should().BeApproximately(3612, 0.01);
        relative.Should().BeTrue();
    }

    [Test]
    public void FollowAfterReopen_ShouldHoldPin_UntilDemuxCatchesUp()
    {
        var hold = true;
        VlcTime.FollowAfterReopen(0, 120, ref hold).Should().Be(120);
        hold.Should().BeTrue();

        VlcTime.FollowAfterReopen(118.5, 120, ref hold).Should().BeApproximately(118.5, 0.01);
        hold.Should().BeFalse();
    }

    [Test]
    public void FollowAfterReopen_ShouldPassThrough_WhenHoldCleared()
    {
        var hold = false;
        VlcTime.FollowAfterReopen(50, 120, ref hold).Should().Be(50);
    }
}
