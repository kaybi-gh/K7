namespace K7.Shared;

/// <summary>
/// Shared display labels for history/stats/player (serie + episode code, artist + track).
/// </summary>
public static class MediaDisplayTitles
{
    public static string FormatEpisode(
        string? serieName,
        string? episodeTitle,
        int seasonNumber,
        int episodeNumber)
    {
        var code = $"S{seasonNumber:D2}E{episodeNumber:D2}";

        if (string.IsNullOrWhiteSpace(episodeTitle))
            return string.IsNullOrWhiteSpace(serieName) ? code : $"{serieName} ({code})";

        return string.IsNullOrWhiteSpace(serieName)
            ? $"{episodeTitle} ({code})"
            : $"{serieName} - {episodeTitle} ({code})";
    }

    public static string FormatTrack(string? artistName, string? trackTitle)
    {
        if (string.IsNullOrWhiteSpace(trackTitle))
            return artistName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(artistName))
            return trackTitle;

        return $"{artistName.Trim()} - {trackTitle.Trim()}";
    }
}
