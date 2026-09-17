using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class TranscodeWipedOutputPolicyTests
{
    [Test]
    public void NeedsReset_ShouldBeFalse_WhenRemuxColdStartHasStagingAndEofTarget()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: true,
                isFfmpegRunning: true,
                isCopyRemux: true,
                hasRemuxStaging: true,
                hasObservedReadyOutput: false,
                lastRequestedSegmentIndex: -1,
                targetSegmentIndex: 1426,
                windowStartIndex: -1,
                generatingFromSegmentIndex: 0,
                bufferSize: 10)
            .Should().BeFalse();
    }

    [Test]
    public void NeedsReset_ShouldBeFalse_WhenRemuxHeadStillStagingAfterFirstMediaGet()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: true,
                isFfmpegRunning: true,
                isCopyRemux: true,
                hasRemuxStaging: true,
                hasObservedReadyOutput: false,
                lastRequestedSegmentIndex: 0,
                targetSegmentIndex: 1426,
                windowStartIndex: -1,
                generatingFromSegmentIndex: 0,
                bufferSize: 10)
            .Should().BeFalse();
    }

    [Test]
    public void NeedsReset_ShouldBeTrue_WhenRemuxDirectoryGoneUnderLiveProcess()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: false,
                isFfmpegRunning: true,
                isCopyRemux: true,
                hasRemuxStaging: false,
                hasObservedReadyOutput: false,
                lastRequestedSegmentIndex: -1,
                targetSegmentIndex: 1426,
                windowStartIndex: -1,
                generatingFromSegmentIndex: 0,
                bufferSize: 10)
            .Should().BeTrue();
    }

    [Test]
    public void NeedsReset_ShouldBeTrue_WhenRemuxStagingGoneUnderLiveProcess()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: true,
                isFfmpegRunning: true,
                isCopyRemux: true,
                hasRemuxStaging: false,
                hasObservedReadyOutput: false,
                lastRequestedSegmentIndex: -1,
                targetSegmentIndex: 1426,
                windowStartIndex: -1,
                generatingFromSegmentIndex: 0,
                bufferSize: 10)
            .Should().BeTrue();
    }

    [Test]
    public void NeedsReset_ShouldBeTrue_WhenRemuxStoppedAndClientLandingLeftOnEmptyCache()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: true,
                isFfmpegRunning: false,
                isCopyRemux: true,
                hasRemuxStaging: false,
                hasObservedReadyOutput: false,
                lastRequestedSegmentIndex: 500,
                targetSegmentIndex: 1426,
                windowStartIndex: -1,
                generatingFromSegmentIndex: 0,
                bufferSize: 10)
            .Should().BeTrue();
    }

    [Test]
    public void NeedsReset_ShouldBeFalse_WhenEncodeColdStartUsesBufferWindow()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: true,
                isFfmpegRunning: true,
                isCopyRemux: false,
                hasRemuxStaging: false,
                hasObservedReadyOutput: false,
                lastRequestedSegmentIndex: -1,
                targetSegmentIndex: 10,
                windowStartIndex: 0,
                generatingFromSegmentIndex: 0,
                bufferSize: 10)
            .Should().BeFalse();
    }

    [Test]
    public void NeedsReset_ShouldBeFalse_WhenEncodeResumeWindowHasNotLandedYet()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: true,
                isFfmpegRunning: true,
                isCopyRemux: false,
                hasRemuxStaging: false,
                hasObservedReadyOutput: false,
                lastRequestedSegmentIndex: 1055,
                targetSegmentIndex: 1065,
                windowStartIndex: 1050,
                generatingFromSegmentIndex: 1050,
                bufferSize: 10)
            .Should().BeFalse();
    }

    [Test]
    public void NeedsReset_ShouldBeTrue_WhenEncodeHadOutputThenCacheWiped()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: true,
                isFfmpegRunning: true,
                isCopyRemux: false,
                hasRemuxStaging: false,
                hasObservedReadyOutput: true,
                lastRequestedSegmentIndex: 1055,
                targetSegmentIndex: 1065,
                windowStartIndex: 1050,
                generatingFromSegmentIndex: 1050,
                bufferSize: 10)
            .Should().BeTrue();
    }

    [Test]
    public void NeedsReset_ShouldBeTrue_WhenEncodeStoppedWithStaleEofTarget()
    {
        TranscodeWipedOutputPolicy.NeedsReset(
                outputDirectoryExists: true,
                isFfmpegRunning: false,
                isCopyRemux: false,
                hasRemuxStaging: false,
                hasObservedReadyOutput: false,
                lastRequestedSegmentIndex: -1,
                targetSegmentIndex: 1408,
                windowStartIndex: 0,
                generatingFromSegmentIndex: 0,
                bufferSize: 10)
            .Should().BeTrue();
    }
}
