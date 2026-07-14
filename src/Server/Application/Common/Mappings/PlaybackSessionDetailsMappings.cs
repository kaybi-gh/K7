using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.Common.Mappings;

public static class PlaybackSessionDetailsMappings
{
    public static StreamDecisionDto? ToStreamDecisionDto(this PlaybackSessionDetails? details)
    {
        if (details is null)
            return null;

        if (details.SourceVideoCodec is null && details.SourceAudioCodec is null)
            return null;

        return new StreamDecisionDto
        {
            Mode = InferPlaybackMode(details),
            Reason = details.TranscodeReason ?? TranscodeReason.None,
            SourceVideoCodec = details.SourceVideoCodec,
            SourceAudioCodec = details.SourceAudioCodec,
            StreamVideoCodec = details.StreamVideoCodec,
            StreamAudioCodec = details.StreamAudioCodec,
            SourceResolution = FormatResolution(details.SourceVideoWidth, details.SourceVideoHeight),
            Bitrate = details.Bitrate,
            AudioTrackLanguage = details.AudioTrackLanguage,
            AudioTrackTitle = details.AudioTrackTitle,
            AudioChannelLayout = details.AudioChannelLayout,
            SubtitleTrackLanguage = details.SubtitleTrackLanguage,
            SubtitleTrackTitle = details.SubtitleTrackTitle,
            IsSubtitleBurnIn = details.TranscodeReason?.HasFlag(TranscodeReason.SubtitlesBurnIn) == true
        };
    }

    private static PlaybackMode InferPlaybackMode(PlaybackSessionDetails details)
    {
        if (details.IsTranscode == true)
            return PlaybackMode.Transcode;

        if (string.Equals(details.VideoDecision, "Transmux", StringComparison.OrdinalIgnoreCase)
            || string.Equals(details.AudioDecision, "Transmux", StringComparison.OrdinalIgnoreCase))
        {
            return PlaybackMode.Transmux;
        }

        return PlaybackMode.Direct;
    }

    private static string? FormatResolution(int? width, int? height) =>
        width is > 0 && height is > 0 ? $"{width}x{height}" : null;
}
