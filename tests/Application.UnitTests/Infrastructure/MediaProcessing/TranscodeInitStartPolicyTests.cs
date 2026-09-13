using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class TranscodeInitStartPolicyTests
{
    [Test]
    public void ResolveStartIndex_ShouldBeZero_WhenCacheEmptyAndNoClientLanding()
    {
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: -1,
                lastClientMediaSegmentRequest: -1,
                segmentCount: 1409)
            .Should().Be(0);
    }

    [Test]
    public void ResolveStartIndex_ShouldUseClientLanding_WhenCacheEmpty()
    {
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: -1,
                lastClientMediaSegmentRequest: 366,
                segmentCount: 1409)
            .Should().Be(366);
    }

    [Test]
    public void ResolveStartIndex_ShouldNotUseEofTarget_WhenCacheEmpty()
    {
        // Previous bug: current=-1 and Target=1408 started ffmpeg at 1398.
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: -1,
                lastClientMediaSegmentRequest: -1,
                segmentCount: 1409)
            .Should().Be(0);
    }

    [Test]
    public void ResolveStartIndex_ShouldPadBackFromReadyMedia()
    {
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: 20,
                lastClientMediaSegmentRequest: 366,
                segmentCount: 1409)
            .Should().Be(15);
    }

    [Test]
    public void ResolveStartIndex_ShouldBeZero_WhenSegmentCountIsNotPositive()
    {
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: 20,
                lastClientMediaSegmentRequest: 366,
                segmentCount: 0)
            .Should().Be(0);
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: -1,
                lastClientMediaSegmentRequest: 8,
                segmentCount: -1)
            .Should().Be(0);
    }

    [Test]
    public void ResolveStartIndex_ShouldClampClientLanding_WhenPastLastSegment()
    {
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: -1,
                lastClientMediaSegmentRequest: 2000,
                segmentCount: 1409)
            .Should().Be(1408);
    }

    [Test]
    public void ResolveStartIndex_ShouldClampPad_WhenNearStartOrEnd()
    {
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: 0,
                lastClientMediaSegmentRequest: -1,
                segmentCount: 1409)
            .Should().Be(0);
        TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex: 1408,
                lastClientMediaSegmentRequest: -1,
                segmentCount: 1409)
            .Should().Be(1403);
    }
}
