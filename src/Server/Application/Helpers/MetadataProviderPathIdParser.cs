using System.Text.RegularExpressions;
using K7.Server.Application.Common;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Parses provider ids from media paths, e.g. [tmdbid-123], [tvdbid-456], [imdbid-tt123].
/// </summary>
public static partial class MetadataProviderPathIdParser
{
    [GeneratedRegex(
        @"\[(?<provider>tmdbid|tvdbid|imdbid|tmdb|tvdb|imdb)[\s_-]*(?<id>[^\]\s]+)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ProviderIdToken();

    public static (string? ProviderName, string? ExternalId) TryParse(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return (null, null);

        var match = ProviderIdToken().Match(pathOrName);
        if (!match.Success)
            return (null, null);

        var providerToken = match.Groups["provider"].Value.Trim().ToLowerInvariant();
        var externalId = match.Groups["id"].Value.Trim();
        if (string.IsNullOrWhiteSpace(externalId))
            return (null, null);

        var providerName = providerToken switch
        {
            "tmdbid" or "tmdb" => MetadataProviderNames.Tmdb,
            "tvdbid" or "tvdb" => MetadataProviderNames.Tvdb,
            "imdbid" or "imdb" => MetadataProviderNames.Imdb,
            _ => null
        };

        return (providerName, externalId);
    }

    public static (string? ProviderName, string? ExternalId) TryParseFromPaths(params string?[] paths)
    {
        foreach (var path in paths)
        {
            var parsed = TryParse(path);
            if (!string.IsNullOrWhiteSpace(parsed.ProviderName) && !string.IsNullOrWhiteSpace(parsed.ExternalId))
                return parsed;
        }

        return (null, null);
    }

    public static string StripProviderIdTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        var stripped = ProviderIdToken().Replace(value, " ");
        return Regex.Replace(stripped, @"\s{2,}", " ").Trim().TrimEnd('-', '.', '_', ' ');
    }
}
