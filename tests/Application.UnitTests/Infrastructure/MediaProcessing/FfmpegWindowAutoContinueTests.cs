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
}
