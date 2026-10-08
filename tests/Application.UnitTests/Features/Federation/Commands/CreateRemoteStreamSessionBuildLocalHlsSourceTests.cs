using K7.Server.Application.Features.Federation.Commands.CreateRemoteStreamSession;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.Federation.Commands;

[TestFixture]
public class CreateRemoteStreamSessionBuildLocalHlsSourceTests
{
    [Test]
    public void ShouldServePeerHls_ShouldBeFalse_WhenDecisionIsDirect()
    {
        CreateRemoteStreamSessionCommandHandler.ShouldServePeerHls(
                FederatedPlaybackExecution.Peer,
                new StreamDecisionDto { Mode = PlaybackMode.Direct })
            .Should().BeFalse();
    }

    [Test]
    public void ShouldServePeerHls_ShouldBeTrue_WhenDecisionIsRemux()
    {
        CreateRemoteStreamSessionCommandHandler.ShouldServePeerHls(
                FederatedPlaybackExecution.Peer,
                new StreamDecisionDto { Mode = PlaybackMode.Transmux })
            .Should().BeTrue();
    }

    [Test]
    public void BuildLocalHlsSource_ShouldOmitTranscodingVideoCodec_WhenRemux()
    {
        var sessionId = Guid.NewGuid();
        var uri = CreateRemoteStreamSessionCommandHandler.BuildLocalHlsSource(
            sessionId,
            new StreamDecisionDto
            {
                Mode = PlaybackMode.Transmux,
                SourceVideoCodec = "hevc",
                StreamAudioCodec = "aac",
                SourceAudioCodec = "eac3",
                SelectedAudioTrackIndex = 2
            });

        var text = uri.Uri.OriginalString;
        text.Should().Contain($"/api/remote-stream-sessions/{sessionId}/hls-stream/manifest.m3u8");
        text.Should().Contain("VideoCodecsOnly=true");
        text.Should().Contain("DefaultAudioTrackIndex=2");
        text.Should().NotContain("TranscodingVideoCodec=");
        text.Should().NotContain("Quality=");
    }

    [Test]
    public void BuildLocalHlsSource_ShouldIncludeTranscodingVideoCodecAndQuality_WhenDownscale()
    {
        var sessionId = Guid.NewGuid();
        var uri = CreateRemoteStreamSessionCommandHandler.BuildLocalHlsSource(
            sessionId,
            new StreamDecisionDto
            {
                Mode = PlaybackMode.Transcode,
                StreamVideoCodec = "h264",
                StreamResolution = "1280x720",
                Reason = TranscodeReason.QualityDownscale
            });

        var text = uri.Uri.OriginalString;
        text.Should().Contain("TranscodingVideoCodec=h264");
        text.Should().Contain("Quality=720p");
        text.Should().Contain("VideoCodecsOnly=true");
    }
}
