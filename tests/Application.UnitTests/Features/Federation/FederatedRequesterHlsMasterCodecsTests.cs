using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.Federation;

[TestFixture]
public class FederatedRequesterHlsMasterCodecsTests
{
    [Test]
    public void ResolveFederatedHlsAudio_ShouldCopy_WhenDirectDecisionCodecMatchesSource()
    {
        var (codec, channels) = FederatedRequesterHlsService.ResolveFederatedHlsAudio(
            new K7.Shared.Dtos.StreamDecisionDto
            {
                Mode = PlaybackMode.Direct,
                SourceAudioCodec = "ac3",
                StreamAudioCodec = "ac3"
            },
            queryCodec: null,
            queryChannels: null);

        codec.Should().BeNull();
        channels.Should().BeNull();
    }

    [Test]
    public void ResolveAudioTrackIndex_ShouldPreferRequestedIndex_WhenDecisionIsMissing()
    {
        FederatedRequesterHlsService.ResolveAudioTrackIndex(
                requestedIndex: 2,
                decisionIndex: null)
            .Should().Be(2);
    }

    [Test]
    public void ResolveAudioTrackIndex_ShouldPreferRequestedIndex_WhenDecisionIsZero()
    {
        FederatedRequesterHlsService.ResolveAudioTrackIndex(
                requestedIndex: 2,
                decisionIndex: 0)
            .Should().Be(2);
    }

    [Test]
    public void ResolveAudioTrackIndex_ShouldUseDecision_WhenRequestIsMissing()
    {
        FederatedRequesterHlsService.ResolveAudioTrackIndex(
                requestedIndex: null,
                decisionIndex: 2)
            .Should().Be(2);
    }

    [Test]
    public void ResolveAudioTrackIndex_ShouldStayNull_WhenBothMissing()
    {
        FederatedRequesterHlsService.ResolveAudioTrackIndex(
                requestedIndex: null,
                decisionIndex: null)
            .Should().BeNull();
    }

    [Test]
    public void ApplyFederatedBurnIn_ShouldKeepAudioIndex_WhenPgsIndexIsSet()
    {
        var current = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transcode,
            SelectedAudioTrackIndex = 2,
            SourceAudioCodec = "ac3",
            StreamAudioCodec = "aac",
            StreamAudioChannels = 2,
            StreamVideoCodec = "h264"
        };

        var updated = FederatedRequesterHlsService.ApplyFederatedBurnIn(
            current,
            queryHasBurnIn: true,
            burnInIndex: 4,
            clientPlaybackUrl: true);

        updated!.IsSubtitleBurnIn.Should().BeTrue();
        updated.SelectedSubtitleTrackIndex.Should().Be(4);
        updated.SelectedAudioTrackIndex.Should().Be(2);
        updated.StreamVideoCodec.Should().Be("h264");
        updated.Reason.HasFlag(TranscodeReason.SubtitlesBurnIn).Should().BeTrue();
    }

    [Test]
    public void ApplyFederatedBurnIn_ShouldClear_WhenPlaybackUrlOmitsIndex()
    {
        var current = new StreamDecisionDto
        {
            IsSubtitleBurnIn = true,
            SelectedSubtitleTrackIndex = 4,
            SelectedAudioTrackIndex = 2,
            StreamVideoCodec = "h264"
        };

        var updated = FederatedRequesterHlsService.ApplyFederatedBurnIn(
            current,
            queryHasBurnIn: false,
            burnInIndex: 0,
            clientPlaybackUrl: true);

        updated!.IsSubtitleBurnIn.Should().BeFalse();
        updated.SelectedSubtitleTrackIndex.Should().BeNull();
        updated.SelectedAudioTrackIndex.Should().Be(2);
    }

    [Test]
    public void ApplyFederatedBurnIn_ShouldKeep_WhenUrlIsNotAPlaybackUrl()
    {
        var current = new StreamDecisionDto
        {
            IsSubtitleBurnIn = true,
            SelectedSubtitleTrackIndex = 4
        };

        var updated = FederatedRequesterHlsService.ApplyFederatedBurnIn(
            current,
            queryHasBurnIn: false,
            burnInIndex: 0,
            clientPlaybackUrl: false);

        updated.Should().BeSameAs(current);
    }

    [Test]
    public void ResolveMasterStreamInfCodecs_ShouldAdvertiseSourceVideo_WhenRemuxAndVideoCodecsOnly()
    {
        var audio = HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, audioCodec: "aac");
        var codecs = FederatedRequesterHlsService.ResolveMasterStreamInfCodecs(
            transcodingVideoCodec: null,
            sourceVideoCodec: "hevc",
            audioCodecs: audio,
            videoCodecsOnly: true);

        codecs.Should().Be(HlsCodecStringHelpers.GetHlsCodecs("hevc", audioCodec: null));
        codecs.Should().NotContain("mp4a");
    }

    [Test]
    public void ResolveMasterStreamInfCodecs_ShouldNotBeAudioOnly_WhenRemuxWithoutVideoCodecsOnly()
    {
        var audio = HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, audioCodec: "aac");
        var codecs = FederatedRequesterHlsService.ResolveMasterStreamInfCodecs(
            transcodingVideoCodec: null,
            sourceVideoCodec: "h264",
            audioCodecs: audio,
            videoCodecsOnly: false);

        codecs.Should().StartWith("avc1.");
        codecs.Should().Contain("mp4a");
    }

    [Test]
    public void ResolveMasterStreamInfCodecs_ShouldPreferTranscodingVideo_WhenEncode()
    {
        var codecs = FederatedRequesterHlsService.ResolveMasterStreamInfCodecs(
            transcodingVideoCodec: "h264",
            sourceVideoCodec: "hevc",
            audioCodecs: "mp4a.40.2",
            videoCodecsOnly: true);

        codecs.Should().Be(HlsCodecStringHelpers.GetHlsCodecs("h264", audioCodec: null));
    }

    [Test]
    public void ResolveNonLadderVideoCodec_ShouldKeepNull_WhenRemux()
    {
        var codec = FederatedRequesterHlsService.ResolveNonLadderVideoCodec(
            videoCodecFromQuery: null,
            mode: PlaybackMode.Transmux,
            streamVideoCodec: "hevc",
            usedEqualLengthFallback: false);

        codec.Should().BeNull();
    }

    [Test]
    public void ResolveNonLadderVideoCodec_ShouldUseStreamVideoCodec_WhenTranscode()
    {
        var codec = FederatedRequesterHlsService.ResolveNonLadderVideoCodec(
            videoCodecFromQuery: null,
            mode: PlaybackMode.Transcode,
            streamVideoCodec: "h264",
            usedEqualLengthFallback: false);

        codec.Should().Be("h264");
    }

    [Test]
    public void ResolveNonLadderVideoCodec_ShouldForceH264_WhenEqualLengthFallback()
    {
        var codec = FederatedRequesterHlsService.ResolveNonLadderVideoCodec(
            videoCodecFromQuery: null,
            mode: PlaybackMode.Transmux,
            streamVideoCodec: null,
            usedEqualLengthFallback: true);

        codec.Should().Be(HlsSegmentHelper.FallbackTranscodingVideoCodec);
    }

    [Test]
    public void ResolveServeTfdtRebasePolicy_ShouldAlignVideoEncodeTightly()
    {
        var (tolerance, align) = FederatedRequesterHlsService.ResolveServeTfdtRebasePolicy(
            isAudioOnly: false,
            isVideoEncode: true);

        tolerance.Should().Be(Hls.VideoTfdtAlignToleranceMs);
        align.Should().BeTrue();
    }

    [Test]
    public void ResolveServeTfdtRebasePolicy_ShouldUseWindowThreshold_ForAudio()
    {
        var (tolerance, align) = FederatedRequesterHlsService.ResolveServeTfdtRebasePolicy(
            isAudioOnly: true,
            isVideoEncode: false);

        tolerance.Should().Be(Hls.TfdtWindowResetThresholdMs);
        align.Should().BeFalse();
    }

}
