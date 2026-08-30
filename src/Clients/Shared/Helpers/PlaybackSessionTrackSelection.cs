using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.Shared.Helpers;

public static class PlaybackSessionTrackSelection
{
    public static void Apply(
        IReadOnlyList<AudioFileTrackDto> audioTracks,
        IReadOnlyList<SubtitleFileTrackDto> subtitleTracks,
        PlaybackSettingsDto settings,
        int? requestedAudioTrackIndex,
        int? requestedSubtitleTrackIndex,
        out AudioFileTrackDto? audio,
        out SubtitleFileTrackDto? subtitle)
    {
        var honorCaller = requestedAudioTrackIndex is not null || requestedSubtitleTrackIndex is not null;

        if (honorCaller)
        {
            audio = requestedAudioTrackIndex is int audioIdx
                ? audioTracks.FirstOrDefault(t => t.Index == audioIdx)
                    ?? audioTracks.FirstOrDefault(t => t.IsDefault)
                    ?? audioTracks.FirstOrDefault()
                : audioTracks.FirstOrDefault(t => t.Index == settings.AudioTrackIndex)
                    ?? audioTracks.FirstOrDefault(t => t.IsDefault)
                    ?? audioTracks.FirstOrDefault();

            subtitle = requestedSubtitleTrackIndex is int subIdx
                ? subtitleTracks.FirstOrDefault(t => t.Index == subIdx)
                : null;
            return;
        }

        audio = audioTracks.FirstOrDefault(t => t.Index == settings.AudioTrackIndex)
            ?? audioTracks.FirstOrDefault(t => t.IsDefault)
            ?? audioTracks.FirstOrDefault();

        subtitle = settings.SubtitleTrackIndex is int sessionSub
            ? subtitleTracks.FirstOrDefault(t => t.Index == sessionSub)
            : null;
    }
}
