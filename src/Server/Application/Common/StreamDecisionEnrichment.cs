using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.Common;

public static class StreamDecisionEnrichment
{
    public static bool RequiresVideoEncoder(StreamDecisionDto decision)
    {
        if (string.IsNullOrEmpty(decision.StreamVideoCodec))
            return false;

        if (decision.Mode == PlaybackMode.Transcode)
            return true;

        if (decision.IsSubtitleBurnIn || decision.Reason.HasFlag(TranscodeReason.SubtitlesBurnIn))
            return true;

        if (decision.SourceVideoCodec is not null
            && !string.Equals(decision.SourceVideoCodec, decision.StreamVideoCodec, StringComparison.OrdinalIgnoreCase))
            return true;

        return decision.Reason.HasFlag(TranscodeReason.VideoCodecNotSupported)
            || decision.Reason.HasFlag(TranscodeReason.ResolutionNotSupported)
            || decision.Reason.HasFlag(TranscodeReason.QualityDownscale)
            || decision.Reason.HasFlag(TranscodeReason.HlsSegmentsUnavailable);
    }

    public static bool RequiresAudioEncoder(StreamDecisionDto decision)
    {
        if (string.IsNullOrEmpty(decision.StreamAudioCodec))
            return false;

        if (decision.SourceAudioCodec is not null
            && !string.Equals(decision.SourceAudioCodec, decision.StreamAudioCodec, StringComparison.OrdinalIgnoreCase))
            return true;

        if (decision.Reason.HasFlag(TranscodeReason.AudioCodecNotSupported))
            return true;

        return decision.Mode == PlaybackMode.Transcode && decision.SourceVideoCodec is null;
    }

    public static async Task<StreamDecisionDto> EnrichEncodersAsync(
        StreamDecisionDto decision,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        CancellationToken cancellationToken = default)
    {
        decision = await EnrichVideoEncoderAsync(decision, ffmpegCapabilitiesService, cancellationToken);
        return EnrichAudioEncoder(decision);
    }

    public static async Task<StreamDecisionDto> EnrichVideoEncoderAsync(
        StreamDecisionDto decision,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        CancellationToken cancellationToken = default)
    {
        if (!RequiresVideoEncoder(decision))
            return decision;

        if (decision.VideoEncoder is not null && decision.IsHardwareAccelerated.HasValue)
            return decision;

        var forceSoftware = decision.IsSubtitleBurnIn
            || decision.Reason.HasFlag(TranscodeReason.SubtitlesBurnIn);

        var encoder = await ResolveEncoderWithFallbackAsync(
            decision.StreamVideoCodec!,
            forceSoftware,
            ffmpegCapabilitiesService,
            cancellationToken);

        if (encoder is null)
            return decision;

        return decision with
        {
            VideoEncoder = encoder.EncoderName,
            IsHardwareAccelerated = encoder.IsHardwareAccelerated
        };
    }

    public static StreamDecisionDto EnrichAudioEncoder(StreamDecisionDto decision)
    {
        if (!RequiresAudioEncoder(decision))
            return decision;

        if (decision.AudioEncoder is not null)
            return decision;

        var encoder = FfmpegAudioEncoderResolver.ResolveEncoderName(decision.StreamAudioCodec!);
        if (encoder is null)
            return decision;

        return decision with { AudioEncoder = encoder };
    }

    public static async Task TryEnrichAndUpdateTrackerAsync(
        Guid sessionId,
        IActiveStreamTracker tracker,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        CancellationToken cancellationToken = default)
    {
        var info = tracker.GetStreamInfo(sessionId);
        if (info?.StreamDecision is not { } decision)
            return;

        var enriched = await EnrichEncodersAsync(decision, ffmpegCapabilitiesService, cancellationToken);
        if (enriched.VideoEncoder == decision.VideoEncoder
            && enriched.IsHardwareAccelerated == decision.IsHardwareAccelerated
            && enriched.AudioEncoder == decision.AudioEncoder)
        {
            return;
        }

        tracker.UpdateStreamDecision(sessionId, enriched);
    }

    private static async Task<VideoEncoderInfoDto?> ResolveEncoderWithFallbackAsync(
        string logicalCodec,
        bool forceSoftware,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        CancellationToken cancellationToken)
    {
        var encoder = await ffmpegCapabilitiesService.ResolveVideoEncoderAsync(
            logicalCodec,
            forceSoftware,
            cancellationToken);

        if (encoder is not null)
            return encoder;

        if (string.Equals(logicalCodec, "h264", StringComparison.OrdinalIgnoreCase)
            || string.Equals(logicalCodec, "hevc", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await ffmpegCapabilitiesService.ResolveVideoEncoderAsync("h264", forceSoftware, cancellationToken);
    }
}
