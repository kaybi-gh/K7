using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Common;

public class StreamDecisionEnrichmentTests
{
    [Test]
    public void RequiresVideoEncoder_ShouldReturnFalse_WhenAudioOnlyTransmux()
    {
        var decision = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transmux,
            Reason = TranscodeReason.AudioCodecNotSupported,
            SourceVideoCodec = "hevc",
            StreamVideoCodec = "hevc",
            SourceAudioCodec = "dts",
            StreamAudioCodec = "aac"
        };

        StreamDecisionEnrichment.RequiresVideoEncoder(decision).Should().BeFalse();
        StreamDecisionEnrichment.RequiresAudioEncoder(decision).Should().BeTrue();
    }

    [Test]
    public void RequiresAudioEncoder_ShouldReturnTrue_WhenMusicTranscode()
    {
        var decision = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transcode,
            Reason = TranscodeReason.AudioCodecNotSupported,
            SourceAudioCodec = "flac",
            StreamAudioCodec = "aac"
        };

        StreamDecisionEnrichment.RequiresAudioEncoder(decision).Should().BeTrue();
    }

    [Test]
    public void EnrichAudioEncoder_ShouldMapLogicalCodecToFfmpegEncoder()
    {
        var decision = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transmux,
            Reason = TranscodeReason.AudioCodecNotSupported,
            SourceAudioCodec = "dts",
            StreamAudioCodec = "opus"
        };

        var enriched = StreamDecisionEnrichment.EnrichAudioEncoder(decision);

        enriched.AudioEncoder.Should().Be("libopus");
    }

    [Test]
    public void RequiresVideoEncoder_ShouldReturnTrue_WhenSubtitleBurnInOnTransmux()
    {
        var decision = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transmux,
            Reason = TranscodeReason.SubtitlesBurnIn,
            SourceVideoCodec = "hevc",
            StreamVideoCodec = "h264",
            IsSubtitleBurnIn = true
        };

        StreamDecisionEnrichment.RequiresVideoEncoder(decision).Should().BeTrue();
    }

    [Test]
    public async Task EnrichEncodersAsync_ShouldPopulateVideoAndAudioEncoders()
    {
        var ffmpeg = Substitute.For<IFfmpegCapabilitiesService>();
        ffmpeg.ResolveVideoEncoderAsync("h264", false, Arg.Any<CancellationToken>())
            .Returns(new VideoEncoderInfoDto
            {
                EncoderName = "h264_nvenc",
                IsHardwareAccelerated = true
            });

        var decision = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transcode,
            Reason = TranscodeReason.VideoCodecNotSupported | TranscodeReason.AudioCodecNotSupported,
            SourceVideoCodec = "hevc",
            StreamVideoCodec = "h264",
            SourceAudioCodec = "dts",
            StreamAudioCodec = "aac"
        };

        var enriched = await StreamDecisionEnrichment.EnrichEncodersAsync(decision, ffmpeg);

        enriched.VideoEncoder.Should().Be("h264_nvenc");
        enriched.IsHardwareAccelerated.Should().BeTrue();
        enriched.AudioEncoder.Should().Be("aac");
    }

    [Test]
    public async Task TryEnrichAndUpdateTrackerAsync_ShouldPersistEnrichedDecision()
    {
        var sessionId = Guid.NewGuid();
        var tracker = Substitute.For<IActiveStreamTracker>();
        var ffmpeg = Substitute.For<IFfmpegCapabilitiesService>();

        var decision = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transcode,
            StreamVideoCodec = "h264"
        };

        tracker.GetStreamInfo(sessionId).Returns(new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user",
            StartedAt = DateTime.UtcNow,
            StreamDecision = decision
        });

        ffmpeg.ResolveVideoEncoderAsync("h264", false, Arg.Any<CancellationToken>())
            .Returns(new VideoEncoderInfoDto
            {
                EncoderName = "libx264",
                IsHardwareAccelerated = false
            });

        await StreamDecisionEnrichment.TryEnrichAndUpdateTrackerAsync(sessionId, tracker, ffmpeg);

        tracker.Received(1).UpdateStreamDecision(
            sessionId,
            Arg.Is<StreamDecisionDto>(d =>
                d.VideoEncoder == "libx264"
                && d.IsHardwareAccelerated == false));
    }
}
