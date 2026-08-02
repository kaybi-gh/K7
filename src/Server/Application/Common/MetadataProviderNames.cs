namespace K7.Server.Application.Common;

/// <summary>
/// Canonical metadata provider name strings. Used for library config, external ids, and Metadata-lane
/// background task admission keys.
/// </summary>
public static class MetadataProviderNames
{
    public const string Tmdb = "tmdb";
    public const string Imdb = "imdb";
    public const string Tvdb = "tvdb";
    public const string MusicBrainz = "musicbrainz";
    public const string Wikidata = "wikidata";
    public const string Wikimedia = "wikimedia";
    public const string CoverArt = "coverart";
    public const string Local = "local";
    public const string DefaultLanguage = "en";

    /// <summary>Stable list for Metadata-lane admission UI and settings stats.</summary>
    public static readonly IReadOnlyList<string> AdmissionKeys =
    [
        Tmdb,
        Tvdb,
        MusicBrainz,
        Wikidata,
        Wikimedia,
        CoverArt,
        Local
    ];

    public static string Normalize(string providerName) =>
        providerName switch
        {
            Imdb => Tmdb,
            _ => providerName
        };
}
