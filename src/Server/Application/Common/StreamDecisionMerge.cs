using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common;

public static class StreamDecisionMerge
{
    public static bool HasTrackDetails(this StreamDecisionDto? decision) =>
        decision is not null
        && (decision.SourceVideoCodec is not null || decision.SourceAudioCodec is not null);

    public static StreamDecisionDto Merge(StreamDecisionDto? baseline, StreamDecisionDto fresh)
    {
        if (baseline is null)
            return fresh;

        return fresh with
        {
            StreamResolution = baseline.StreamResolution ?? fresh.StreamResolution,
            Reason = baseline.Reason != TranscodeReason.None ? baseline.Reason : fresh.Reason,
            VideoEncoder = baseline.VideoEncoder ?? fresh.VideoEncoder,
            AudioEncoder = baseline.AudioEncoder ?? fresh.AudioEncoder,
            IsHardwareAccelerated = baseline.IsHardwareAccelerated ?? fresh.IsHardwareAccelerated,
            Bitrate = baseline.Bitrate ?? fresh.Bitrate
        };
    }
}
