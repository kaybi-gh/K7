using K7.Server.Domain.Entities;
using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class FfmpegRemuxSeekPolicyTests
{
    [Test]
    public void ShouldSpawnRemuxHead_ShouldBeFalse_WhenSegmentAlreadyReady()
    {
        FfmpegRemuxSeekPolicy.ShouldSpawnRemuxHead(
                remuxCopy: true,
                segmentReady: true,
                coveredByLiveHead: false,
                minDistanceSecondsToLiveHead: double.PositiveInfinity)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldSpawnRemuxHead_ShouldBeFalse_WhenCoveredByLiveHead()
    {
        FfmpegRemuxSeekPolicy.ShouldSpawnRemuxHead(
                remuxCopy: true,
                segmentReady: false,
                coveredByLiveHead: true,
                minDistanceSecondsToLiveHead: 0)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldSpawnRemuxHead_ShouldBeFalse_WhenNearLiveTip()
    {
        FfmpegRemuxSeekPolicy.ShouldSpawnRemuxHead(
                remuxCopy: true,
                segmentReady: false,
                coveredByLiveHead: false,
                minDistanceSecondsToLiveHead: 30)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldSpawnRemuxHead_ShouldBeTrue_WhenFarFromLiveTip()
    {
        FfmpegRemuxSeekPolicy.ShouldSpawnRemuxHead(
                remuxCopy: true,
                segmentReady: false,
                coveredByLiveHead: false,
                minDistanceSecondsToLiveHead: 90)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldSpawnRemuxHead_ShouldBeTrue_WhenNoLiveHeads()
    {
        FfmpegRemuxSeekPolicy.ShouldSpawnRemuxHead(
                remuxCopy: true,
                segmentReady: false,
                coveredByLiveHead: false,
                minDistanceSecondsToLiveHead: double.PositiveInfinity)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldSpawnRemuxHead_ShouldBeFalse_WhenEncodeWindowed()
    {
        FfmpegRemuxSeekPolicy.ShouldSpawnRemuxHead(
                remuxCopy: false,
                segmentReady: false,
                coveredByLiveHead: false,
                minDistanceSecondsToLiveHead: double.PositiveInfinity)
            .Should().BeFalse();
    }

    [Test]
    public void MinDistanceSecondsToLiveHead_ShouldBeZero_WhenTipHasReachedIndex()
    {
        var segments = BuildFourSecondSegments(20);
        var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
            requestedIndex: 3,
            liveHeads: [(FromIndex: 0, TipIndex: 5, UntilInclusive: 19, Running: true)],
            segments);

        distance.Should().Be(0);
    }

    [Test]
    public void MinDistanceSecondsToLiveHead_ShouldMeasureForwardGap_WhenAheadOfTipInsideTarget()
    {
        // Ahead of the tip but still inside the head's EOF target window: not covered.
        // The head has only produced up to segment 5, so a request at 8 is 3 * 4s = 12s away.
        var segments = BuildFourSecondSegments(20);
        var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
            requestedIndex: 8,
            liveHeads: [(FromIndex: 0, TipIndex: 5, UntilInclusive: 19, Running: true)],
            segments);

        distance.Should().Be(12);
    }

    [Test]
    public void MinDistanceSecondsToLiveHead_ShouldMeasureGapToTip()
    {
        var segments = BuildFourSecondSegments(20);
        var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
            requestedIndex: 15,
            liveHeads: [(FromIndex: 0, TipIndex: 5, UntilInclusive: 10, Running: true)],
            segments);

        // Indices 5..15 exclusive end at 15 => 10 segments * 4s = 40s
        distance.Should().Be(40);
    }

    [Test]
    public void IsClientSeekJump_ShouldDetectResumeMidFile()
    {
        FfmpegRemuxSeekPolicy.IsClientSeekJump(previousClientRequest: -1, requestedIndex: 8)
            .Should().BeTrue();
        FfmpegRemuxSeekPolicy.IsClientSeekJump(previousClientRequest: -1, requestedIndex: 0)
            .Should().BeFalse();
    }

    [Test]
    public void IsClientSeekJump_ShouldIgnoreLinearLookahead()
    {
        FfmpegRemuxSeekPolicy.IsClientSeekJump(previousClientRequest: 20, requestedIndex: 25)
            .Should().BeFalse();
        FfmpegRemuxSeekPolicy.IsClientSeekJump(previousClientRequest: 20, requestedIndex: 35)
            .Should().BeTrue();
    }

    [Test]
    public void MinDistanceSecondsToLiveHead_ShouldIgnoreStoppedHeads()
    {
        var segments = BuildFourSecondSegments(20);
        var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
            requestedIndex: 15,
            liveHeads:
            [
                (FromIndex: 0, TipIndex: 5, UntilInclusive: 19, Running: false),
                (FromIndex: 0, TipIndex: 12, UntilInclusive: 19, Running: true)
            ],
            segments);

        distance.Should().Be(12);
    }

    [Test]
    public void MinDistanceSecondsToLiveHead_ShouldBeInfinity_WhenRequestIsBehindEveryHeadStart()
    {
        // Restart from the beginning while the resume head runs at 113 (tip 130): no head
        // will ever write segment 0, so it must spawn instead of waiting "covered".
        var segments = BuildFourSecondSegments(200);
        var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
            requestedIndex: 0,
            liveHeads: [(FromIndex: 113, TipIndex: 130, UntilInclusive: 199, Running: true)],
            segments);

        distance.Should().Be(double.PositiveInfinity);
        FfmpegRemuxSeekPolicy.ShouldSpawnRemuxHead(
                remuxCopy: true,
                segmentReady: false,
                coveredByLiveHead: false,
                distance)
            .Should().BeTrue();
    }

    [Test]
    public void MinDistanceSecondsToLiveHead_ShouldBeInfinity_WhenNoRunningHeads()
    {
        var segments = BuildFourSecondSegments(20);
        var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
            requestedIndex: 8,
            liveHeads: [(FromIndex: 0, TipIndex: 5, UntilInclusive: 19, Running: false)],
            segments);

        distance.Should().Be(double.PositiveInfinity);
    }

    [Test]
    public void IsClientSeekJump_ShouldBeTrue_WhenSeekingBackward()
    {
        FfmpegRemuxSeekPolicy.IsClientSeekJump(previousClientRequest: 80, requestedIndex: 20)
            .Should().BeTrue();
    }

    private static List<HlsSegment> BuildFourSecondSegments(int count)
    {
        var list = new List<HlsSegment>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(new HlsSegment
            {
                StartTimestamp = i * 4000L,
                Duration = 4000
            });
        }

        return list;
    }
}
