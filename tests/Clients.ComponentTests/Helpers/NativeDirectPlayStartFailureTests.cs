using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativeDirectPlayStartFailureTests
{
    [Test]
    public void ShouldFallbackQualityLadder_ShouldBeTrue_WhenRuntimeCheckAtStart()
    {
        NativeDirectPlayStartFailure
            .ShouldFallbackQualityLadder(
                "PlayerErrorCode=1004 PlayerErrorCodeName=ERROR_CODE_FAILED_RUNTIME_CHECK",
                positionSeconds: 0,
                isLocalFile: false)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldFallbackQualityLadder_ShouldBeTrue_WhenAlreadyOnTranscodedQuality()
    {
        NativeDirectPlayStartFailure
            .ShouldFallbackQualityLadder(
                "ERROR_CODE_FAILED_RUNTIME_CHECK",
                positionSeconds: 0,
                isLocalFile: false)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldFallbackQualityLadder_ShouldBeFalse_WhenOfflineFile()
    {
        NativeDirectPlayStartFailure
            .ShouldFallbackQualityLadder(
                "ERROR_CODE_FAILED_RUNTIME_CHECK",
                positionSeconds: 0,
                isLocalFile: true)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldFallbackQualityLadder_ShouldBeFalse_WhenMidPlayback()
    {
        NativeDirectPlayStartFailure
            .ShouldFallbackQualityLadder(
                "ERROR_CODE_FAILED_RUNTIME_CHECK",
                positionSeconds: 12,
                isLocalFile: false)
            .Should().BeFalse();
    }

    [Test]
    public void LooksLikeDecoderOrRuntimeCheck_ShouldBeFalse_WhenTimeoutOnly()
    {
        NativeDirectPlayStartFailure
            .LooksLikeDecoderOrRuntimeCheck("ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldRetrySameDirectPlay_ShouldBeTrue_WhenFirstRuntimeCheckOnDirectPlay()
    {
        NativeDirectPlayStartFailure
            .ShouldRetrySameDirectPlay(
                "PlayerErrorCode=1004 PlayerErrorCodeName=ERROR_CODE_FAILED_RUNTIME_CHECK",
                positionSeconds: 0,
                isLocalFile: false,
                isDirectPlay: true,
                retryCount: 0)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldRetrySameDirectPlay_ShouldBeFalse_WhenAlreadyRetried()
    {
        NativeDirectPlayStartFailure
            .ShouldRetrySameDirectPlay(
                "ERROR_CODE_FAILED_RUNTIME_CHECK",
                positionSeconds: 0,
                isLocalFile: false,
                isDirectPlay: true,
                retryCount: 1)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldRetrySameDirectPlay_ShouldBeFalse_WhenHls()
    {
        NativeDirectPlayStartFailure
            .ShouldRetrySameDirectPlay(
                "ERROR_CODE_FAILED_RUNTIME_CHECK",
                positionSeconds: 0,
                isLocalFile: false,
                isDirectPlay: false,
                retryCount: 0)
            .Should().BeFalse();
    }
}
