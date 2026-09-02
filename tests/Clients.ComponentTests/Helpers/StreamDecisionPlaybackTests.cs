using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class StreamDecisionPlaybackTests
{
    [Test]
    public void Align_ShouldStayDirect_WhenDirectStreamUrl()
    {
        var aligned = StreamDecisionPlayback.Align(
            new StreamDecisionDto { Mode = PlaybackMode.Direct, SourceVideoCodec = "hevc" },
            "https://k7/api/stream/direct-stream?id=1",
            "video/x-matroska",
            isOriginalQuality: true);

        aligned!.Mode.Should().Be(PlaybackMode.Direct);
    }

    [Test]
    public void Align_ShouldBecomeTransmux_WhenHlsOriginalAfterDirect()
    {
        var aligned = StreamDecisionPlayback.Align(
            new StreamDecisionDto { Mode = PlaybackMode.Direct, SourceVideoCodec = "hevc" },
            "https://k7/api/hls-stream/manifest.m3u8",
            "application/vnd.apple.mpegurl",
            isOriginalQuality: true);

        aligned!.Mode.Should().Be(PlaybackMode.Transmux);
    }

    [Test]
    public void Align_ShouldBecomeTranscode_WhenQualityStep()
    {
        var aligned = StreamDecisionPlayback.Align(
            new StreamDecisionDto { Mode = PlaybackMode.Transmux, SourceVideoCodec = "hevc" },
            "https://k7/api/hls-stream/manifest.m3u8?Quality=720p",
            "application/vnd.apple.mpegurl",
            isOriginalQuality: false);

        aligned!.Mode.Should().Be(PlaybackMode.Transcode);
        aligned.Reason.Should().HaveFlag(TranscodeReason.QualityDownscale);
    }

    [Test]
    public void OverallMode_ShouldBeTranscode_WhenAudioCodecChanges()
    {
        var mode = StreamDecisionPlayback.OverallMode(
            new StreamDecisionDto
            {
                Mode = PlaybackMode.Transmux,
                SourceVideoCodec = "hevc",
                StreamVideoCodec = "hevc",
                SourceAudioCodec = "eac3",
                StreamAudioCodec = "aac"
            },
            StreamDecisionHudLabels.English);

        mode.Should().Be("Transcode");
    }
}
