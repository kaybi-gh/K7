using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class DolbyVisionDecodePolicyTests
{
    [Test]
    public void DefaultForDevice_ShouldBeHevcHdr10_WhenAndroidTelevision()
    {
        DolbyVisionDecodePolicy
            .DefaultForDevice(isTelevision: true, "NVIDIA", "SHIELD Android TV")
            .Should().Be(DolbyVisionDecodeMode.HevcHdr10);
        DolbyVisionDecodePolicy
            .DefaultForDevice(isTelevision: true, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().Be(DolbyVisionDecodeMode.HevcHdr10);
    }

    [Test]
    public void DefaultForDevice_ShouldBeHevcHdr10_WhenAmlogicEvenIfNotTelevision()
    {
        DolbyVisionDecodePolicy
            .DefaultForDevice(isTelevision: false, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().Be(DolbyVisionDecodeMode.HevcHdr10);
    }

    [Test]
    public void DefaultForDevice_ShouldBeNative_WhenPhone()
    {
        DolbyVisionDecodePolicy
            .DefaultForDevice(isTelevision: false, "Google", "Pixel 8")
            .Should().Be(DolbyVisionDecodeMode.Native);
    }

    [Test]
    public void Resolve_ShouldHonorStoredNativeOnTelevision()
    {
        DolbyVisionDecodePolicy
            .Resolve("native", isTelevision: true, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().Be(DolbyVisionDecodeMode.Native);
    }

    [Test]
    public void ShouldPreferHevcDecoderForDolbyVision_ShouldFollowMode()
    {
        AndroidExoPlaybackPolicy
            .ShouldPreferHevcDecoderForDolbyVision(DolbyVisionDecodeMode.HevcHdr10)
            .Should().BeTrue();
        AndroidExoPlaybackPolicy
            .ShouldPreferHevcDecoderForDolbyVision(DolbyVisionDecodeMode.Native)
            .Should().BeFalse();
    }
}
