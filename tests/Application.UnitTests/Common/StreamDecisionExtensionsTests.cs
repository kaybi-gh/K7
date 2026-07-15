using K7.Server.Application.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Common;

public class StreamDecisionExtensionsTests
{
    [Test]
    public void ApplyQualityDownscale_ShouldMarkVideoTranscode_WhenDownscaling()
    {
        var existing = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transmux,
            Reason = TranscodeReason.AudioCodecNotSupported,
            SourceVideoCodec = "hevc",
            StreamVideoCodec = "hevc",
            SourceResolution = "1920x1080"
        };

        var updated = StreamDecisionExtensions.ApplyQualityDownscale(
            existing,
            Constants.VideoQualities[VideoResolutionIdentifier._720p],
            "h264",
            "1920x1080");

        updated.Mode.Should().Be(PlaybackMode.Transcode);
        updated.Reason.Should().HaveFlag(TranscodeReason.QualityDownscale);
        updated.Reason.Should().NotHaveFlag(TranscodeReason.ResolutionNotSupported);
        updated.StreamVideoCodec.Should().Be("h264");
        updated.StreamResolution.Should().Be("1280x720");
    }
}
