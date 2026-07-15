using FluentAssertions;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Common.Mappings;

public class PlaybackSessionDetailsMappingsTests
{
    [Test]
    public void ToStreamDecisionDto_ShouldMapCodecsAndTracks_WhenDetailsArePresent()
    {
        var details = new PlaybackSessionDetails
        {
            IsTranscode = true,
            VideoDecision = "Transcode",
            AudioDecision = "Transcode",
            TranscodeReason = TranscodeReason.AudioCodecNotSupported,
            SourceVideoCodec = "hevc",
            SourceAudioCodec = "dts",
            StreamVideoCodec = "h264",
            StreamAudioCodec = "aac",
            SourceVideoWidth = 1920,
            SourceVideoHeight = 1080,
            Bitrate = 8000,
            AudioTrackLanguage = "fra",
            AudioTrackTitle = "French",
            AudioChannelLayout = "5.1",
            SubtitleTrackLanguage = "eng",
            SubtitleTrackTitle = "English"
        };

        var decision = details.ToStreamDecisionDto();

        decision.Should().NotBeNull();
        decision!.Mode.Should().Be(PlaybackMode.Transcode);
        decision.Reason.Should().Be(TranscodeReason.AudioCodecNotSupported);
        decision.SourceVideoCodec.Should().Be("hevc");
        decision.StreamVideoCodec.Should().Be("h264");
        decision.SourceAudioCodec.Should().Be("dts");
        decision.StreamAudioCodec.Should().Be("aac");
        decision.SourceResolution.Should().Be("1920x1080");
        decision.Bitrate.Should().Be(8000);
        decision.AudioTrackLanguage.Should().Be("fra");
        decision.SubtitleTrackTitle.Should().Be("English");
    }

    [Test]
    public void ToStreamDecisionDto_ShouldReturnNull_WhenOnlyTranscodeFlagIsSet()
    {
        var details = new PlaybackSessionDetails
        {
            IsTranscode = true,
            VideoDecision = "Transcode",
            AudioDecision = "Transcode"
        };

        details.ToStreamDecisionDto().Should().BeNull();
    }

    [Test]
    public void ToStreamDecisionDto_ShouldReturnNull_WhenDetailsHaveNoStreamInfo()
    {
        var details = new PlaybackSessionDetails();

        details.ToStreamDecisionDto().Should().BeNull();
    }
}
