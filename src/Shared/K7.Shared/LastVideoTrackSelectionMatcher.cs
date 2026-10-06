using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Shared;

public static class LastVideoTrackSelectionMatcher
{
    public static TrackSelector.TrackSelectionResult? TryMatch(
        LastVideoTrackSelectionDto selection,
        IReadOnlyList<AudioFileTrackDto> audioTracks,
        IReadOnlyList<SubtitleFileTrackDto> subtitleTracks)
    {
        if (audioTracks.Count == 0)
            return null;

        var audioIndex = MatchAudioIndex(audioTracks, selection.AudioLanguage);
        if (audioIndex is null)
            return null;

        if (selection.SubtitlesOff)
            return new TrackSelector.TrackSelectionResult(audioIndex.Value, null);

        var subtitleIndex = MatchSubtitleIndex(
            subtitleTracks,
            selection.SubtitleLanguage,
            selection.IsForced,
            selection.IsHearingImpaired);

        return new TrackSelector.TrackSelectionResult(audioIndex.Value, subtitleIndex);
    }

    private static int? MatchAudioIndex(IReadOnlyList<AudioFileTrackDto> tracks, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        for (var i = 0; i < tracks.Count; i++)
        {
            if (string.Equals(tracks[i].Language, language, StringComparison.OrdinalIgnoreCase))
                return tracks[i].Index;
        }

        return null;
    }

    private static int? MatchSubtitleIndex(
        IReadOnlyList<SubtitleFileTrackDto> tracks,
        string? language,
        bool isForced,
        bool isHearingImpaired)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        SubtitleFileTrackDto? languageOnly = null;
        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (!string.Equals(track.Language, language, StringComparison.OrdinalIgnoreCase))
                continue;

            if (track.IsForced == isForced && track.IsHearingImpaired == isHearingImpaired)
                return track.Index;

            languageOnly ??= track;
        }

        return languageOnly?.Index;
    }
}
