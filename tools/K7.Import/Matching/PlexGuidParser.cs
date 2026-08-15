using System.Text.RegularExpressions;

namespace K7.Import.Matching;

/// <summary>
/// Parses Plex / Tautulli guid strings into K7 provider ids (tmdb, imdb, tvdb, musicbrainz).
/// </summary>
internal static partial class PlexGuidParser
{
    public static void TryAdd(Dictionary<string, string> providerIds, string? raw, string? plexType)
    {
        if (!TryParse(raw, plexType, out var provider, out var value))
            return;

        providerIds.TryAdd(provider, value);
    }

    public static bool TryParse(string? raw, string? plexType, out string provider, out string value)
    {
        provider = "";
        value = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var match = GuidRegex().Match(raw.Trim());
        if (!match.Success)
            return false;

        var scheme = match.Groups[1].Value.ToLowerInvariant();
        value = match.Groups[2].Value;
        var queryIndex = value.IndexOf('?');
        if (queryIndex >= 0)
            value = value[..queryIndex];

        const string agentPrefix = "com.plexapp.agents.";
        if (scheme.StartsWith(agentPrefix, StringComparison.Ordinal))
            scheme = scheme[agentPrefix.Length..];

        if (scheme is "plex" or "none")
            return false;

        provider = scheme switch
        {
            "themoviedb" or "tmdb" => "tmdb",
            "thetvdb" or "tvdb" or "thetvdbdvdorder" => "tvdb",
            "imdb" => "imdb",
            "mbid" or "musicbrainz" => plexType is "track" or "music" ? "musicbrainz" : "musicbrainz-release",
            _ => scheme
        };

        // Old Plex TVDB episode guids are seriesId/season/episode, not the episode's TVDB id.
        if (provider is "tvdb" && value.Contains('/'))
            return false;

        if (provider is "imdb"
            && !value.StartsWith("tt", StringComparison.OrdinalIgnoreCase)
            && value.All(char.IsDigit))
        {
            value = "tt" + value;
        }

        return provider.Length > 0 && value.Length > 0;
    }

    [GeneratedRegex(@"^([a-zA-Z0-9.]+)://(.+)$")]
    private static partial Regex GuidRegex();
}
