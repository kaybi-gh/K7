using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.Shared.Helpers;

public static class PlaybackTrackContinuity
{
    public static int? MatchAudioIndex(
        IEnumerable<AudioFileTrackDto>? tracks,
        AudioFileTrackDto? current)
    {
        if (current is null || tracks is null)
            return null;

        if (!string.IsNullOrWhiteSpace(current.Language))
        {
            var byLanguage = tracks.FirstOrDefault(t =>
                string.Equals(t.Language, current.Language, StringComparison.OrdinalIgnoreCase));
            if (byLanguage is not null)
                return byLanguage.Index;
        }

        return tracks.FirstOrDefault(t => t.Index == current.Index)?.Index;
    }

    public static int? MatchSubtitleIndex(
        IEnumerable<SubtitleFileTrackDto>? tracks,
        SubtitleFileTrackDto? current)
    {
        if (current is null || tracks is null)
            return null;

        if (!string.IsNullOrWhiteSpace(current.Language))
        {
            var byLanguage = tracks.FirstOrDefault(t =>
                string.Equals(t.Language, current.Language, StringComparison.OrdinalIgnoreCase)
                && t.IsForced == current.IsForced
                && t.IsHearingImpaired == current.IsHearingImpaired)
                ?? tracks.FirstOrDefault(t =>
                    string.Equals(t.Language, current.Language, StringComparison.OrdinalIgnoreCase));
            if (byLanguage is not null)
                return byLanguage.Index;
        }

        return tracks.FirstOrDefault(t => t.Index == current.Index)?.Index;
    }
}
