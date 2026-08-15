using System.Text.RegularExpressions;
using K7.Import.Models;

namespace K7.Import.Matching;

/// <summary>
/// Groups music items that are the same recording (artist + title, edition suffixes folded)
/// so dry-run / reports list one virtual instead of every Spotify edition.
/// Covers stay distinct: different artist, same title, do not group.
/// </summary>
public static partial class MusicItemCollapser
{
    public static string IdentityKey(SourceMediaItem item)
    {
        var artist = NormalizeArtist(item.ArtistName);
        var title = StripEdition(item.Title);
        return string.IsNullOrEmpty(artist)
            ? title.ToLowerInvariant()
            : $"{artist}|{title.ToLowerInvariant()}";
    }

    public static ItemMatchResult PickCreateRepresentative(IEnumerable<ItemMatchResult> group) =>
        group
            .OrderByDescending(r => r.Item.Popularity ?? int.MinValue)
            .ThenByDescending(r => r.Item.ProviderIds.ContainsKey("isrc") ? 1 : 0)
            .ThenByDescending(r => r.Item.PlayCount)
            .ThenBy(r => r.Item.Id, StringComparer.Ordinal)
            .First();

    public static IReadOnlyList<ItemMatchResult> DistinctCreates(IEnumerable<ItemMatchResult> results)
    {
        var music = new List<ItemMatchResult>();
        var other = new List<ItemMatchResult>();
        foreach (var result in results)
        {
            if (result.Item.MediaType == "music")
                music.Add(result);
            else
                other.Add(result);
        }

        var keepers = music
            .GroupBy(r => IdentityKey(r.Item), StringComparer.OrdinalIgnoreCase)
            .Select(PickCreateRepresentative);

        return [.. other, .. keepers];
    }

    private static string NormalizeArtist(string? artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return "";

        var trimmed = CollapseWhitespaceRegex().Replace(artistName.Trim(), " ");
        if (trimmed.StartsWith("The ", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 4)
            trimmed = trimmed[4..];

        return trimmed.ToLowerInvariant();
    }

    private static string StripEdition(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var trimmed = CollapseWhitespaceRegex().Replace(title.Trim(), " ");
        var stripped = TrackEditionSuffixRegex().Replace(trimmed, "").Trim();
        return stripped.Length == 0 ? trimmed : stripped;
    }

    [GeneratedRegex(
        @"(?:\s*(?:-|\u2013|\u2014)\s*|\s*[\(\[])(original(\s+version)?|album(\s+version)?|remaster(ed)?(\s+\d{4})?)[\)\]]?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TrackEditionSuffixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();
}
