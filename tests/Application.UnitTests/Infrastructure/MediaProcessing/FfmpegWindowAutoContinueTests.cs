using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class FfmpegWindowAutoContinueTests
{
    [Test]
    public void ShouldContinueTowardClientTarget_ShouldBeTrue_WhenTargetPastReadySegments()
    {
        FfmpegWindowAutoContinue.ShouldContinueTowardClientTarget(
                currentSegmentIndex: 9,
                targetSegmentIndex: 20,
                segmentCount: 100)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldContinueTowardClientTarget_ShouldBeFalse_WhenTargetCaughtUp()
    {
        FfmpegWindowAutoContinue.ShouldContinueTowardClientTarget(
                currentSegmentIndex: 9,
                targetSegmentIndex: 9,
                segmentCount: 100)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldContinueTowardClientTarget_ShouldBeFalse_WhenFileComplete()
    {
        FfmpegWindowAutoContinue.ShouldContinueTowardClientTarget(
                currentSegmentIndex: 99,
                targetSegmentIndex: 120,
                segmentCount: 100)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldContinueTowardClientTarget_ShouldBeFalse_WhenNoReadySegments()
    {
        FfmpegWindowAutoContinue.ShouldContinueTowardClientTarget(
                currentSegmentIndex: -1,
                targetSegmentIndex: 10,
                segmentCount: 100)
            .Should().BeFalse();
    }

    [Test]
    public void ResolveContinueTarget_ShouldStretchToBuffer_WhenOldRequestTargetIsOneAhead()
    {
        FfmpegWindowAutoContinue.ResolveContinueTarget(
                startSegmentIndex: 41,
                currentTargetSegmentIndex: 41,
                bufferSize: 10,
                segmentCount: 200)
            .Should().Be(51);
    }

    [Test]
    public void ResolveContinueTarget_ShouldKeepLargerClientTarget()
    {
        FfmpegWindowAutoContinue.ResolveContinueTarget(
                startSegmentIndex: 41,
                currentTargetSegmentIndex: 80,
                bufferSize: 10,
                segmentCount: 200)
            .Should().Be(80);
    }

    [Test]
    public void ResolveContinueTarget_ShouldClampToLastIndex_WhenNearEof()
    {
        FfmpegWindowAutoContinue.ResolveContinueTarget(
                startSegmentIndex: 95,
                currentTargetSegmentIndex: 96,
                bufferSize: 10,
                segmentCount: 100)
            .Should().Be(99);
    }

    [Test]
    public void ShouldKeepLookahead_ShouldBeTrue_WhenReadyCaughtTargetAndClientIsNearFrontier()
    {
        FfmpegWindowAutoContinue.ShouldKeepLookahead(
                currentSegmentIndex: 11,
                targetSegmentIndex: 10,
                lastRequestedSegmentIndex: 8,
                bufferSize: 10,
                segmentCount: 200)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldKeepLookahead_ShouldBeFalse_WhenClientPausedFarBehindReady()
    {
        FfmpegWindowAutoContinue.ShouldKeepLookahead(
                currentSegmentIndex: 40,
                targetSegmentIndex: 40,
                lastRequestedSegmentIndex: 5,
                bufferSize: 10,
                segmentCount: 200)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldKeepLookahead_ShouldBeFalse_WhenTargetAlreadyAhead()
    {
        FfmpegWindowAutoContinue.ShouldKeepLookahead(
                currentSegmentIndex: 10,
                targetSegmentIndex: 20,
                lastRequestedSegmentIndex: 10,
                bufferSize: 10,
                segmentCount: 200)
            .Should().BeFalse();
    }

    [Test]
    public void ResolveAdvertisedTarget_ShouldGoToLastIndex_WhenRemuxToEnd()
    {
        FfmpegWindowAutoContinue.ResolveAdvertisedTarget(
                requestedSegmentIndex: 12,
                currentTargetSegmentIndex: 10,
                bufferSize: 10,
                segmentCount: 200,
                remuxToEnd: true)
            .Should().Be(199);
    }

    [Test]
    public void ResolveAdvertisedTarget_ShouldStayOnBufferWindow_WhenEncode()
    {
        FfmpegWindowAutoContinue.ResolveAdvertisedTarget(
                requestedSegmentIndex: 12,
                currentTargetSegmentIndex: 10,
                bufferSize: 10,
                segmentCount: 200,
                remuxToEnd: false)
            .Should().Be(22);
    }
}
