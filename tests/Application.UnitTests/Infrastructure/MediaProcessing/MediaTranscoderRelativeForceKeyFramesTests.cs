using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class MediaTranscoderRelativeForceKeyFramesTests
{
    [Test]
    public void ShouldUseRelativeForceKeyFrames_ShouldBeTrue_WhenBurnIn()
    {
        MediaTranscoder.ShouldUseRelativeForceKeyFrames(
                hasBurnIn: true,
                videoFilterWillRun: false)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldUseRelativeForceKeyFrames_ShouldBeTrue_WhenScaleFilter()
    {
        MediaTranscoder.ShouldUseRelativeForceKeyFrames(
                hasBurnIn: false,
                videoFilterWillRun: true)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldUseRelativeForceKeyFrames_ShouldBeFalse_WhenLocalRemuxEncodeWithoutFilter()
    {
        MediaTranscoder.ShouldUseRelativeForceKeyFrames(
                hasBurnIn: false,
                videoFilterWillRun: false)
            .Should().BeFalse();
    }
}
