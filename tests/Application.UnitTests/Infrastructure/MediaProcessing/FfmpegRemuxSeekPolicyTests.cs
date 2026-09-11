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
    public void MinDistanceSecondsToLiveHead_ShouldBeZero_WhenInsideRunningWindow()
    {
        var segments = BuildFourSecondSegments(20);
        var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
            requestedIndex: 8,
            liveHeads: [(TipIndex: 5, UntilInclusive: 19, Running: true)],
            segments);

        distance.Should().Be(0);
    }

    [Test]
    public void MinDistanceSecondsToLiveHead_ShouldMeasureGapToTip()
    {
        var segments = BuildFourSecondSegments(20);
        var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
            requestedIndex: 15,
            liveHeads: [(TipIndex: 5, UntilInclusive: 10, Running: true)],
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
