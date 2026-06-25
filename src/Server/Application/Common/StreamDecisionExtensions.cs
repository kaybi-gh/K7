using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.Common;

public static class StreamDecisionExtensions
{
    public static int? GetSubtitleStreamOrdinal(IEnumerable<SubtitleFileTrack> tracks, int absoluteStreamIndex)
    {
        var ordered = tracks.OrderBy(t => t.Index).ToList();
        var idx = ordered.FindIndex(t => t.Index == absoluteStreamIndex);
        return idx >= 0 ? idx : null;
    }

    public static StreamDecisionDto ApplySubtitleBurnIn(StreamDecisionDto? existing, SubtitleFileTrack track)
    {
        var baseDecision = existing ?? new StreamDecisionDto
        {
            Mode = PlaybackMode.Transcode,
            Reason = TranscodeReason.None
        };

        var reason = baseDecision.Reason;
        if (reason == TranscodeReason.ContainerNotSupported && baseDecision.Mode == PlaybackMode.Transmux)
        {
            reason = TranscodeReason.None;
        }

        reason |= TranscodeReason.SubtitlesBurnIn;

        return baseDecision with
        {
            Mode = PlaybackMode.Transcode,
            Reason = reason,
            IsSubtitleBurnIn = true,
            SelectedSubtitleTrackIndex = track.Index,
            SubtitleCodec = track.Codec,
            SubtitleTrackLanguage = track.Language ?? baseDecision.SubtitleTrackLanguage,
            SubtitleTrackTitle = track.Name ?? baseDecision.SubtitleTrackTitle,
            StreamVideoCodec = baseDecision.StreamVideoCodec ?? "h264",
            SourceVideoCodec = baseDecision.SourceVideoCodec
        };
    }
}
