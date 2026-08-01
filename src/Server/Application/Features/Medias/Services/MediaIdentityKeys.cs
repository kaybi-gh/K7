using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Medias.Services;

public static partial class MediaIdentityKeys
{
    public static string NormalizeMovieTitle(string title, int? year) =>
        year is null ? title : $"{title}|{year.Value}";

    public static string NormalizeSerieTitle(string title, int? year) =>
        NormalizeMovieTitle(title, year);

    public static string NormalizeEpisodeKey(string? seriesTitle, int? seasonNumber, int? episodeNumber, string title) =>
        $"{seriesTitle ?? "Unknown Series"}|S{seasonNumber ?? 0}|E{episodeNumber ?? 0}|{title}";

    /// <summary>
    /// Stable music identity key: "Artist - Title". Strips a redundant artist baked into the
    /// title so "When You Know - Puggy" + Puggy and "When You Know" + Puggy share one key.
    /// </summary>
    public static string NormalizeMusicTitle(string? artistName, string title)
    {
        var core = StripRedundantArtistFromTitle(StripFeatureCredits(title), artistName);
        var artist = NormalizePersonName(artistName);
        return artist is not null ? $"{artist} - {core}" : core;
    }

    public static string? NormalizePersonName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = CollapseWhitespaceRegex().Replace(name.Trim(), " ");
        // Drop a leading "The " so "The Beatles" matches "Beatles".
        if (trimmed.StartsWith("The ", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 4)
            trimmed = trimmed[4..];

        return trimmed;
    }

    /// <summary>
    /// Removes a redundant artist prefix/suffix from a title ("When You Know - Puggy" / "Puggy - When You Know").
    /// Matching still uses title core + artist separately; this only cleans the title string.
    /// </summary>
    public static string StripRedundantArtistFromTitle(string title, string? artistName)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var trimmed = CollapseWhitespaceRegex().Replace(title.Trim(), " ");
        var artist = NormalizePersonName(artistName);
        if (artist is null || artist.Length == 0)
            return trimmed;

        var suffix = " - " + artist;
        if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > suffix.Length)
            trimmed = trimmed[..^suffix.Length].TrimEnd();

        var prefix = artist + " - ";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > prefix.Length)
            trimmed = trimmed[prefix.Length..].TrimStart();

        return trimmed;
    }

    public static string NormalizeKey(string part1, string part2) =>
        $"{part1.ToUpperInvariant()}|{part2.ToUpperInvariant()}";

    public static string StripFeatureCredits(string title) =>
        FeatureCreditsRegex().Replace(title, "").Trim();

    [GeneratedRegex(@"\s*[\(\[](feat\.?|ft\.?|with)\s.+?[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex FeatureCreditsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();
}
