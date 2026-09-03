using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativePlaybackStatsFormattingTests
{
    [Test]
    public void PlayMethod_ShouldBeDirectPlay_WhenUrlIsDirectStream()
    {
        NativePlaybackStatsFormatting
            .PlayMethod("https://k7/api/stream/direct-stream?x=1", "video/x-matroska", isOriginal: true)
            .Should().Be("Direct Play");
    }

    [Test]
    public void PlayMethod_ShouldBeRemux_WhenHlsOriginal()
    {
        NativePlaybackStatsFormatting
            .PlayMethod("https://k7/api/hls-stream/manifest.m3u8", "application/vnd.apple.mpegurl", isOriginal: true)
            .Should().Be("Remux HLS");
    }

    [Test]
    public void PlayMethod_ShouldBeTranscode_WhenHlsEncode()
    {
        NativePlaybackStatsFormatting
            .PlayMethod("https://k7/api/hls-stream/manifest.m3u8?Quality=1080p", "application/vnd.apple.mpegurl", isOriginal: false)
            .Should().Be("Transcode HLS");
    }

    [Test]
    public void ShortCodec_ShouldMapDolbyVisionAndHevc()
    {
        NativePlaybackStatsFormatting.ShortCodec("video/dolby-vision").Should().Be("DV");
        NativePlaybackStatsFormatting.ShortCodec("video/hevc").Should().Be("HEVC");
        NativePlaybackStatsFormatting.ShortCodec("audio/eac3").Should().Be("EAC3");
    }

    [Test]
    public void ToHudText_ShouldIncludeCadenceAndSkipEmptyLines()
    {
        var text = NativePlaybackStatsFormatting.ToHudText(new NativePlaybackStatsSnapshot
        {
            PlayMethod = "Direct Play",
            Quality = "Original (1080p)",
            Video = "HEVC  1920x800  23.976 fps",
            Hdmi = "HDMI 1920x1080 @ 60 Hz",
            Cadence = "3:2 pulldown",
            Frames = "drop 4 / draw 1200  skip 0"
        });

        text.Should().Contain("Direct Play  Original (1080p)");
        text.Should().Contain("3:2 pulldown");
        text.Should().NotContain("\n\n");
    }

    [Test]
    public void WithDecision_ShouldMatchAdminStreamCardLayout()
    {
        var snapshot = NativePlaybackStatsFormatting.WithDecision(
            new NativePlaybackStatsSnapshot
            {
                PlayMethod = "Direct Play",
                Quality = "Original (1080p)",
                Video = "HEVC  1920x800  23.976 fps",
                Policy = "tunnel off  host exo  buf default"
            },
            new StreamDecisionDto
            {
                Mode = PlaybackMode.Direct,
                SourceVideoCodec = "hevc",
                StreamVideoCodec = "hevc",
                SourceAudioCodec = "eac3",
                StreamAudioCodec = "eac3",
                SourceResolution = "1920x800",
                AudioTrackLanguage = "fra"
            },
            StreamDecisionHudLabels.English);

        snapshot.Mode.Should().Be("Direct");
        snapshot.VideoDecision.Should().Be("V  HEVC -> HEVC  Direct");
        snapshot.AudioDecision.Should().Be("A  EAC3 -> EAC3  Direct  FRA");
        snapshot.StreamResolution.Should().Be("1920x800");

        var text = NativePlaybackStatsFormatting.ToHudText(snapshot);
        text.Should().StartWith("Direct  Original (1080p)");
        text.Should().Contain("V  HEVC -> HEVC  Direct");
        text.Should().Contain("tunnel off | host exo | buf default");
    }

    [Test]
    public void FormatHdmiModes_ShouldGroupByHeightAndMarkCurrent()
    {
        var text = NativePlaybackStatsFormatting.FormatHdmiModes(
        [
            new HdmiDisplayMode(3840, 2160, 59.94f, IsCurrent: true),
            new HdmiDisplayMode(3840, 2160, 24f, IsCurrent: false),
            new HdmiDisplayMode(1920, 1080, 60f, IsCurrent: false),
            new HdmiDisplayMode(1920, 1080, 24f, IsCurrent: false)
        ]);

        text.Should().Be("2160p: 59.94* 24 | 1080p: 60 24");
    }
}
