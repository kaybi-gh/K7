using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.Common;

internal static class StreamDecisionTrackSelection
{
    public static StreamDecisionDto Apply(
        StreamDecisionDto decision,
        AudioFileTrack? audio,
        SubtitleFileTrack? subtitle,
        bool subtitleSpecified)
    {
        var next = decision;

        if (audio is not null)
        {
            var keepStreamCodec = decision.Mode is PlaybackMode.Transcode or PlaybackMode.Transmux
                && !string.IsNullOrEmpty(decision.StreamAudioCodec)
                && !string.Equals(decision.StreamAudioCodec, decision.SourceAudioCodec, StringComparison.OrdinalIgnoreCase);

            next = next with
            {
                SelectedAudioTrackIndex = audio.Index,
                SourceAudioCodec = audio.Codec ?? next.SourceAudioCodec,
                StreamAudioCodec = keepStreamCodec ? next.StreamAudioCodec : audio.Codec ?? next.StreamAudioCodec,
                AudioTrackLanguage = audio.Language,
                AudioTrackTitle = audio.Name,
                AudioChannelLayout = audio.ChannelLayout
            };
        }

        if (!subtitleSpecified)
            return next;

        if (subtitle is null)
        {
            return next with
            {
                SelectedSubtitleTrackIndex = null,
                SubtitleTrackLanguage = null,
                SubtitleTrackTitle = null,
                SubtitleCodec = null,
                IsSubtitleBurnIn = false
            };
        }

        return next with
        {
            SelectedSubtitleTrackIndex = subtitle.Index,
            SubtitleTrackLanguage = subtitle.Language,
            SubtitleTrackTitle = subtitle.Name,
            SubtitleCodec = subtitle.Codec
        };
    }
}
