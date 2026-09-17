using K7.Server.Domain.Common;

namespace K7.Server.Domain.UnitTests.Common;

[TestFixture]
public class HlsAudioChannelPolicyTests
{
    [Test]
    public void Resolve_ShouldCapToDeviceOutput_WhenBrowserIsStereo()
    {
        // AC3 5.1 -> AAC for a laptop with 2 speakers: deliver 2ch, not 6ch.
        HlsAudioChannelPolicy.Resolve(6, deviceMaxChannels: 2).Should().Be(2);
    }

    [Test]
    public void Resolve_ShouldKeepSurround_WhenDeviceRendersIt()
    {
        HlsAudioChannelPolicy.Resolve(6, deviceMaxChannels: 6).Should().Be(6);
        HlsAudioChannelPolicy.Resolve(6, deviceMaxChannels: 8).Should().Be(6);
        HlsAudioChannelPolicy.Resolve(8, deviceMaxChannels: 8).Should().Be(8);
    }

    [Test]
    public void Resolve_ShouldKeepSource_WhenNoDeviceCap()
    {
        HlsAudioChannelPolicy.Resolve(6, deviceMaxChannels: null).Should().Be(6);
        HlsAudioChannelPolicy.Resolve(2, deviceMaxChannels: null).Should().Be(2);
        HlsAudioChannelPolicy.Resolve(1, deviceMaxChannels: null).Should().Be(1);
        HlsAudioChannelPolicy.Resolve(0, deviceMaxChannels: null).Should().Be(2);
    }

    [Test]
    public void Resolve_ShouldOnlyProduceHlsLayouts()
    {
        // 5.0 -> 5.1 (silent LFE), 7.0 -> 7.1, 3.0 / 4.0 -> stereo.
        HlsAudioChannelPolicy.Resolve(5, null).Should().Be(6);
        HlsAudioChannelPolicy.Resolve(7, null).Should().Be(8);
        HlsAudioChannelPolicy.Resolve(3, null).Should().Be(2);
        HlsAudioChannelPolicy.Resolve(4, null).Should().Be(2);
        HlsAudioChannelPolicy.Resolve(6, deviceMaxChannels: 4).Should().Be(2);
    }

    [Test]
    public void Resolve_ShouldClampToEncoderLimit()
    {
        HlsAudioChannelPolicy.Resolve(8, null, encoderMaxChannels: 6).Should().Be(6);
    }

    [Test]
    public void AudioOutputChannelTokens_ShouldRoundTrip()
    {
        var token = AudioOutputChannelTokens.MaxChannels(6);
        token.Should().Be("achannels:6");
        AudioOutputChannelTokens.TryReadMaxChannels(["video-mp4-aac-h264", token, "vprofile:hevc:main"]).Should().Be(6);
        AudioOutputChannelTokens.TryReadMaxChannels(["video-mp4-aac-h264"]).Should().BeNull();
        AudioOutputChannelTokens.TryReadMaxChannels(null).Should().BeNull();
        AudioOutputChannelTokens.TryReadMaxChannels(["achannels:x", "achannels:0"]).Should().BeNull();
    }
}
