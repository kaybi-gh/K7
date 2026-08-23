using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsSegmentGenerationPolicyTests
{
    [Test]
    public void ShouldRestartForHole_ShouldBeFalse_WhenFfmpegStillRunning()
    {
        HlsSegmentGenerationPolicy.ShouldRestartForHole(
            requestedIndex: 650,
            ffmpegRunning: true,
            missingWithLaterSegments: true).Should().BeFalse();
    }

    [Test]
    public void ShouldRestartForHole_ShouldBeTrue_WhenFfmpegIdleAndLaterSegmentsExist()
    {
        HlsSegmentGenerationPolicy.ShouldRestartForHole(
            requestedIndex: 650,
            ffmpegRunning: false,
            missingWithLaterSegments: true).Should().BeTrue();
    }

    [Test]
    public void ShouldRestartForHole_ShouldBeFalse_WhenRequestedSegmentIsPlaylistStart()
    {
        HlsSegmentGenerationPolicy.ShouldRestartForHole(
            requestedIndex: 0,
            ffmpegRunning: false,
            missingWithLaterSegments: true).Should().BeFalse();
    }

    [Test]
    public void ShouldRestartForHole_ShouldBeFalse_WhenIdleWithoutLaterSegments()
    {
        HlsSegmentGenerationPolicy.ShouldRestartForHole(
            requestedIndex: 650,
            ffmpegRunning: false,
            missingWithLaterSegments: false).Should().BeFalse();
    }
}
