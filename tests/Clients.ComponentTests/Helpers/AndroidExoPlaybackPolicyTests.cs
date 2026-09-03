using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class AndroidExoPlaybackPolicyTests
{
    [Test]
    public void ShouldEnableHdmiTunneling_ShouldBeFalse_WhenNokiaStreamingBox()
    {
        AndroidExoPlaybackPolicy
            .ShouldEnableHdmiTunneling("SEI Robotics", "Nokia Streaming Box 8000")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldEnableHdmiTunneling_ShouldBeFalse_WhenNvidiaShield()
    {
        AndroidExoPlaybackPolicy
            .ShouldEnableHdmiTunneling("NVIDIA", "SHIELD Android TV")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldEnableHdmiTunneling_ShouldBeFalse_WhenPixelPhone()
    {
        AndroidExoPlaybackPolicy
            .ShouldEnableHdmiTunneling("Google", "Pixel 8")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldInstallTunedExoPlayer_ShouldBeTrue_WhenShieldTelevision()
    {
        AndroidExoPlaybackPolicy
            .ShouldInstallTunedExoPlayer(isTelevision: true, "NVIDIA", "SHIELD Android TV")
            .Should().BeTrue();
    }

    [Test]
    public void ShouldInstallTunedExoPlayer_ShouldBeTrue_WhenAmlogicEvenIfNotReportedAsTelevision()
    {
        AndroidExoPlaybackPolicy
            .ShouldInstallTunedExoPlayer(isTelevision: false, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().BeTrue();
    }

    [Test]
    public void ShouldInstallTunedExoPlayer_ShouldBeFalse_WhenPhone()
    {
        AndroidExoPlaybackPolicy
            .ShouldInstallTunedExoPlayer(isTelevision: false, "Google", "Pixel 8")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldDisableVendorVideoAfr_ShouldBeTrue_WhenAmlogic()
    {
        AndroidExoPlaybackPolicy
            .ShouldDisableVendorVideoAfr("SEI Robotics", "Nokia Streaming Box 8000")
            .Should().BeTrue();
        AndroidExoPlaybackPolicy
            .ShouldDisableVendorVideoAfr("NVIDIA", "SHIELD Android TV")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldPreferContentHdmiResolution_ShouldBeTrue_WhenScaleOnTv()
    {
        AndroidExoPlaybackPolicy
            .ShouldPreferContentHdmiResolution(HdmiAutoFrameRateMode.ScaleOnTv)
            .Should().BeTrue();
        AndroidExoPlaybackPolicy
            .ShouldPreferContentHdmiResolution(HdmiAutoFrameRateMode.ScaleOnDevice)
            .Should().BeFalse();
        AndroidExoPlaybackPolicy
            .ShouldPreferContentHdmiResolution(HdmiAutoFrameRateMode.Disabled)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldApplyHdmiAutoFrameRate_ShouldHonorModeAndDevice()
    {
        AndroidExoPlaybackPolicy
            .ShouldApplyHdmiAutoFrameRate(
                HdmiAutoFrameRateMode.ScaleOnDevice,
                isTelevision: true,
                "NVIDIA",
                "SHIELD Android TV")
            .Should().BeTrue();
        AndroidExoPlaybackPolicy
            .ShouldApplyHdmiAutoFrameRate(
                HdmiAutoFrameRateMode.ScaleOnTv,
                isTelevision: false,
                "SEI Robotics",
                "Nokia Streaming Box 8000")
            .Should().BeTrue();
        AndroidExoPlaybackPolicy
            .ShouldApplyHdmiAutoFrameRate(
                HdmiAutoFrameRateMode.Disabled,
                isTelevision: true,
                "SEI Robotics",
                "Nokia Streaming Box 8000")
            .Should().BeFalse();
        AndroidExoPlaybackPolicy
            .ShouldApplyHdmiAutoFrameRate(
                HdmiAutoFrameRateMode.ScaleOnTv,
                isTelevision: false,
                "Google",
                "Pixel 8")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldAllowSurfaceFrameRateChanges_ShouldBeOffWhenAfrDisabled()
    {
        AndroidExoPlaybackPolicy
            .ShouldAllowSurfaceFrameRateChanges(
                HdmiAutoFrameRateMode.Disabled,
                isTelevision: true,
                "SEI Robotics",
                "Nokia Streaming Box 8000")
            .Should().BeFalse();
        AndroidExoPlaybackPolicy
            .ShouldAllowSurfaceFrameRateChanges(
                HdmiAutoFrameRateMode.ScaleOnDevice,
                isTelevision: true,
                "NVIDIA",
                "SHIELD Android TV")
            .Should().BeTrue();
        AndroidExoPlaybackPolicy
            .ShouldAllowSurfaceFrameRateChanges(
                HdmiAutoFrameRateMode.ScaleOnTv,
                isTelevision: false,
                "Google",
                "Pixel 8")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldEnableAudioOffload_ShouldBeTrue_WhenTelevisionOrAmlogic()
    {
        AndroidExoPlaybackPolicy
            .ShouldEnableAudioOffload(isTelevision: true, "NVIDIA", "SHIELD Android TV")
            .Should().BeTrue();
        AndroidExoPlaybackPolicy
            .ShouldEnableAudioOffload(isTelevision: false, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().BeTrue();
        AndroidExoPlaybackPolicy
            .ShouldEnableAudioOffload(isTelevision: false, "Google", "Pixel 8")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldEnableAudioOffloadForSpeed_ShouldStayOn_AtNormalSpeed()
    {
        AndroidExoPlaybackPolicy.ShouldEnableAudioOffloadForSpeed(policyOffloadEnabled: true, 1.0)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldEnableAudioOffloadForSpeed_ShouldTurnOff_WhenSpeedNotNormal()
    {
        AndroidExoPlaybackPolicy.ShouldEnableAudioOffloadForSpeed(policyOffloadEnabled: true, 1.5)
            .Should().BeFalse();
        AndroidExoPlaybackPolicy.ShouldEnableAudioOffloadForSpeed(policyOffloadEnabled: true, 0.5)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldEnableAudioOffloadForSpeed_ShouldStayOff_WhenPolicyDisabled()
    {
        AndroidExoPlaybackPolicy.ShouldEnableAudioOffloadForSpeed(policyOffloadEnabled: false, 1.0)
            .Should().BeFalse();
    }

    [Test]
    public void IsPreferredVendorDecoder_ShouldBeTrue_WhenNvidiaOrAmlogicHw()
    {
        AndroidExoPlaybackPolicy.IsPreferredVendorDecoder("c2.nvidia.hevc.decoder").Should().BeTrue();
        AndroidExoPlaybackPolicy.IsPreferredVendorDecoder("c2.amlogic.video.decoder.avc").Should().BeTrue();
        AndroidExoPlaybackPolicy.IsPreferredVendorDecoder("OMX.Nvidia.h265.decode").Should().BeTrue();
    }

    [Test]
    public void IsPreferredVendorDecoder_ShouldBeFalse_WhenGoogleSoftwareHevc()
    {
        AndroidExoPlaybackPolicy.IsPreferredVendorDecoder("c2.android.hevc.decoder").Should().BeFalse();
        AndroidExoPlaybackPolicy.IsPreferredVendorDecoder("OMX.google.h265.decoder").Should().BeFalse();
    }
}
