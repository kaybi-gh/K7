using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Medias.Services;

/// <summary>
/// Repairs artist lists that TagLib splits on bare "/" for ID3v2.3 (so "AC/DC" becomes
/// "AC" + "DC"), then re-splits only on space-aware separators.
/// </summary>
public static partial class MusicArtistNameNormalizer
{
    /// <summary>
    /// Rebuilds values TagLib already split on bare "/", then applies <see cref="Split"/>.
    /// Call only for ID3v2.3 (and older) frames; true multi-value tags must stay as-is.
    /// </summary>
    public static IReadOnlyList<string> FromId3v23SplitValues(IEnumerable<string>? values)
    {
        if (values is null)
            return [];

        // Keep leading/trailing spaces so "Alice " + "/" + " Bob" stays "Alice / Bob".
        var parts = values
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (parts.Count == 0)
            return [];

        if (parts.Count == 1)
            return Split(parts[0]);

        return Split(string.Join("/", parts));
    }

    /// <summary>
    /// Splits multi-artist strings on " / " or ";" while keeping names like "AC/DC" intact.
    /// </summary>
    public static IReadOnlyList<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return ArtistSeparatorRegex()
            .Split(value)
            .Select(static s => s.Trim())
            .Where(static s => s.Length > 0)
            .ToList();
    }

    [GeneratedRegex(@"\s+/\s+|;\s*", RegexOptions.CultureInvariant)]
    private static partial Regex ArtistSeparatorRegex();
}
