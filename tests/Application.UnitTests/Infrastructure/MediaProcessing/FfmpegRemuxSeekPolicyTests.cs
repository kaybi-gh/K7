using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class FfmpegRemuxSeekPolicyTests
{
    [Test]
    public void ShouldKeepRunningProcess_ShouldBeTrue_WhenSegmentAlreadyReady()
    {
        FfmpegRemuxSeekPolicy.ShouldKeepRunningProcess(
                remuxToEnd: true,
                segmentReady: true,
                ffmpegRunning: true,
                requestedIndex: 12,
                generatingFrom: 0,
                generatingUntil: 199)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldKeepRunningProcess_ShouldBeTrue_WhenSeekForwardStillInRunningWindow()
    {
        FfmpegRemuxSeekPolicy.ShouldKeepRunningProcess(
                remuxToEnd: true,
                segmentReady: false,
                ffmpegRunning: true,
                requestedIndex: 80,
                generatingFrom: 0,
                generatingUntil: 199)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldKeepRunningProcess_ShouldBeFalse_WhenSeekBeforeRemuxStart()
    {
        FfmpegRemuxSeekPolicy.ShouldKeepRunningProcess(
                remuxToEnd: true,
                segmentReady: false,
                ffmpegRunning: true,
                requestedIndex: 10,
                generatingFrom: 50,
                generatingUntil: 199)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldKeepRunningProcess_ShouldBeFalse_WhenEncodeWindowed()
    {
        FfmpegRemuxSeekPolicy.ShouldKeepRunningProcess(
                remuxToEnd: false,
                segmentReady: false,
                ffmpegRunning: true,
                requestedIndex: 80,
                generatingFrom: 0,
                generatingUntil: 10)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldKeepRunningProcess_ShouldBeFalse_WhenFfmpegIdleAndSegmentMissing()
    {
        FfmpegRemuxSeekPolicy.ShouldKeepRunningProcess(
                remuxToEnd: true,
                segmentReady: false,
                ffmpegRunning: false,
                requestedIndex: 80,
                generatingFrom: 0,
                generatingUntil: 199)
            .Should().BeFalse();
    }
}
