using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Medias.Services;

public static partial class MediaIdentityKeys
{
    public static string NormalizeMovieTitle(string title, int? year) =>
        year is null ? title : $"{title}|{year.Value}";

    public static string NormalizeEpisodeKey(string? seriesTitle, int? seasonNumber, int? episodeNumber, string title) =>
        $"{seriesTitle ?? "Unknown Series"}|S{seasonNumber ?? 0}|E{episodeNumber ?? 0}|{title}";

    public static string NormalizeMusicTitle(string? artistName, string title) =>
        artistName is not null ? $"{artistName} - {title}" : title;

    public static string NormalizeKey(string part1, string part2) =>
        $"{part1.ToUpperInvariant()}|{part2.ToUpperInvariant()}";

    public static string StripFeatureCredits(string title) =>
        FeatureCreditsRegex().Replace(title, "").Trim();

    [GeneratedRegex(@"\s*[\(\[](feat\.?|ft\.?|with)\s.+?[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex FeatureCreditsRegex();
}
