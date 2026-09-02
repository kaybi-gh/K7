using K7.Server.Domain.Common;

namespace K7.Server.Domain.UnitTests.Common;

[TestFixture]
public class VideoDecoderProfileMatchingTests
{
    [Test]
    public void AllowsDirectPlay_ShouldBeTrue_WhenClientIsNotProfileAware()
    {
        var track = Hevc(profile: "Main 10", bitDepth: 10);
        VideoDecoderProfileMatching.AllowsDirectPlay(["video-matroska-aac-hevc"], track)
            .Should().BeTrue();
    }

    [Test]
    public void AllowsDirectPlay_ShouldAllowMain8_WhenOnlyMainIsAdvertised()
    {
        var track = Hevc(profile: "Main", bitDepth: 8);
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [VideoDecoderProfileTokens.HevcMain],
                track)
            .Should().BeTrue();
    }

    [Test]
    public void AllowsDirectPlay_ShouldRejectMain10_WhenOnlyMainIsAdvertised()
    {
        var track = Hevc(profile: "Main 10", bitDepth: 10);
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [VideoDecoderProfileTokens.HevcMain],
                track)
            .Should().BeFalse();
    }

    [Test]
    public void AllowsDirectPlay_ShouldAllowMain8_WhenOnlyMain10IsAdvertised()
    {
        var track = Hevc(profile: "Main", bitDepth: 8);
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [VideoDecoderProfileTokens.HevcMain10],
                track)
            .Should().BeTrue();
    }

    [Test]
    public void AllowsDirectPlay_ShouldAllowMain10_WhenMain10IsAdvertised()
    {
        var track = Hevc(profile: "Main 10", bitDepth: 10);
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [VideoDecoderProfileTokens.HevcMain, VideoDecoderProfileTokens.HevcMain10],
                track)
            .Should().BeTrue();
    }

    [Test]
    public void AllowsDirectPlay_ShouldRejectDolbyVision_WhenDvTokenMissing()
    {
        var track = Hevc(profile: "dvhe.05", bitDepth: 10);
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [VideoDecoderProfileTokens.HevcMain10],
                track)
            .Should().BeFalse();
    }

    [Test]
    public void AllowsDirectPlay_ShouldReject_WhenLevelExceedsDecoder()
    {
        var track = Hevc(profile: "Main", bitDepth: 8, level: 180, width: 1920, height: 800);
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [
                    VideoDecoderProfileTokens.HevcMain,
                    VideoDecoderProfileTokens.Level("hevc", 153)
                ],
                track)
            .Should().BeFalse();
    }

    [Test]
    public void AllowsDirectPlay_ShouldReject_WhenResolutionExceedsDecoder()
    {
        var track = Hevc(profile: "Main", bitDepth: 8, width: 3840, height: 1600);
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [
                    VideoDecoderProfileTokens.HevcMain,
                    VideoDecoderProfileTokens.MaxResolution("hevc", 1920, 1080)
                ],
                track)
            .Should().BeFalse();
    }

    [Test]
    public void AllowsDirectPlay_ShouldAllow_WhenFfprobeLevelIsUnnormalizedAgainstIdc()
    {
        var track = Hevc(profile: "Main", bitDepth: 8, level: 4, width: 1920, height: 800);
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [
                    VideoDecoderProfileTokens.HevcMain,
                    VideoDecoderProfileTokens.Level("hevc", 120)
                ],
                track)
            .Should().BeTrue();
    }

    [Test]
    public void AllowsDirectPlay_ShouldRejectAv1Main10_WhenOnlyMainIsAdvertised()
    {
        var track = new K7.Server.Domain.Entities.Metadatas.Files.Tracks.VideoFileTrack
        {
            Index = 0,
            Codec = "av1",
            Width = 1920,
            Height = 1080,
            Profile = "Main 10",
            Level = 8,
            BitDepth = 10
        };
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [VideoDecoderProfileTokens.Av1Main],
                track)
            .Should().BeFalse();
    }

    [Test]
    public void AllowsDirectPlay_ShouldAllowOtherCodecs_WhenOnlyHevcTokensExist()
    {
        var track = new K7.Server.Domain.Entities.Metadatas.Files.Tracks.VideoFileTrack
        {
            Index = 0,
            Codec = "h264",
            Width = 1920,
            Height = 1080,
            Profile = "High",
            Level = 40
        };
        VideoDecoderProfileMatching.AllowsDirectPlay(
                [VideoDecoderProfileTokens.HevcMain],
                track)
            .Should().BeTrue();
    }

    private static K7.Server.Domain.Entities.Metadatas.Files.Tracks.VideoFileTrack Hevc(
        string profile,
        int bitDepth,
        int level = 150,
        int width = 1920,
        int height = 800) => new()
        {
            Index = 0,
            Codec = "hevc",
            Width = width,
            Height = height,
            Profile = profile,
            Level = level,
            BitDepth = bitDepth
        };
}
