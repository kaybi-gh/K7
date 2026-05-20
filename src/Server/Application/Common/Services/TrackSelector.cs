using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Enums;

namespace K7.Server.Application.Common.Services;

public static class TrackSelector
{
    public record TrackSelectionResult(int AudioTrackIndex, int? SubtitleTrackIndex);

    public static TrackSelectionResult SelectTracks(
        TrackSelectionPreferencesDto preferences,
        IReadOnlyList<AudioFileTrackDto> audioTracks,
        IReadOnlyList<SubtitleFileTrackDto> subtitleTracks)
    {
        if (audioTracks.Count == 0)
            throw new InvalidOperationException("No audio tracks available.");

        var selectedAudio = FindAudioTrack(audioTracks, preferences.PreferredAudioLanguage)
            ?? (preferences.FallbackAudioLanguage is not null
                ? FindAudioTrack(audioTracks, preferences.FallbackAudioLanguage)
                : null)
            ?? audioTracks.FirstOrDefault(t => t.IsDefault)
            ?? audioTracks[0];

        var audioMatchesPreferred = string.Equals(
            selectedAudio.Language,
            preferences.PreferredAudioLanguage,
            StringComparison.OrdinalIgnoreCase);

        var subtitleMode = audioMatchesPreferred
            ? preferences.SubtitleWhenPreferredAudio
            : preferences.SubtitleWhenOtherAudio;

        var subtitleLanguage = audioMatchesPreferred
            ? preferences.SubtitleLanguageWhenPreferredAudio
            : preferences.SubtitleLanguageWhenOtherAudio;

        var selectedSubtitle = FindSubtitleTrack(subtitleTracks, subtitleLanguage, subtitleMode);

        return new TrackSelectionResult(selectedAudio.Index, selectedSubtitle?.Index);
    }

    private static AudioFileTrackDto? FindAudioTrack(IReadOnlyList<AudioFileTrackDto> tracks, string language)
    {
        for (var i = 0; i < tracks.Count; i++)
        {
            if (string.Equals(tracks[i].Language, language, StringComparison.OrdinalIgnoreCase))
                return tracks[i];
        }

        return null;
    }

    private static SubtitleFileTrackDto? FindSubtitleTrack(
        IReadOnlyList<SubtitleFileTrackDto> tracks,
        string language,
        SubtitlePreference mode)
    {
        if (mode == SubtitlePreference.Off)
            return null;

        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (!string.Equals(track.Language, language, StringComparison.OrdinalIgnoreCase))
                continue;

            var matches = mode switch
            {
                SubtitlePreference.ForcedOnly => track.IsForced && !track.IsHearingImpaired,
                SubtitlePreference.Full => !track.IsForced && !track.IsHearingImpaired,
                SubtitlePreference.HearingImpaired => track.IsHearingImpaired,
                _ => false
            };

            if (matches)
                return track;
        }

        return null;
    }
}
