using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Common;
using K7.Shared.Dtos.Devices;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class DeviceCodecDisplayTests
{
    [Test]
    public void FormatVideo_ShouldExpandHevcProfiles_WhenMainAndMain10AreProbed()
    {
        var summary = new DeviceCodecSummaryDto
        {
            Containers = [],
            AudioCodecs = [],
            VideoCodecs = ["hevc", "h264"],
            VideoProfiles =
            [
                VideoDecoderProfileTokens.HevcMain,
                VideoDecoderProfileTokens.HevcMain10,
                VideoDecoderProfileTokens.Level("hevc", 150),
                VideoDecoderProfileTokens.MaxResolution("hevc", 3840, 2160)
            ]
        };

        var labels = DeviceCodecDisplay.FormatVideo(summary);

        labels.Should().Equal(
            "H.264",
            "HEVC Main (L5.0, 3840x2160)",
            "HEVC Main 10 (L5.0, 3840x2160)");
    }

    [Test]
    public void FormatVideo_ShouldKeepBareHevc_WhenNoProfileTokens()
    {
        var summary = new DeviceCodecSummaryDto
        {
            Containers = [],
            AudioCodecs = [],
            VideoCodecs = ["hevc"]
        };

        DeviceCodecDisplay.FormatVideo(summary).Should().Equal("HEVC");
    }

    [Test]
    public void FormatAudio_ShouldPrettyPrintAc3Family()
    {
        var summary = new DeviceCodecSummaryDto
        {
            Containers = [],
            AudioCodecs = ["aac", "eac3", "ac3", "aacHE"],
            VideoCodecs = []
        };

        DeviceCodecDisplay.FormatAudio(summary).Should().Equal("AAC", "AAC-HE", "AC3", "EAC3");
    }

    [Test]
    public void FormatContainers_ShouldMapMkvAndMatroskaToSameLabel()
    {
        var summary = new DeviceCodecSummaryDto
        {
            Containers = ["mkv", "matroska", "mp4"],
            AudioCodecs = [],
            VideoCodecs = []
        };

        DeviceCodecDisplay.FormatContainers(summary).Should().Equal("MKV", "MP4");
    }

    [Test]
    public void FormatSubtitles_ShouldPrettyPrintWebVtt()
    {
        var summary = new DeviceCodecSummaryDto
        {
            Containers = [],
            AudioCodecs = [],
            VideoCodecs = [],
            SubtitleCodecs = ["webvtt"]
        };

        DeviceCodecDisplay.FormatSubtitles(summary).Should().Equal("WebVTT");
    }
}
