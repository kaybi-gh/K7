namespace K7.Server.Application.Common;

public static class MetadataProviderNames
{
    public const string Tmdb = "tmdb";
    public const string Imdb = "imdb";
    public const string MusicBrainz = "musicbrainz";
    public const string DefaultLanguage = "en";

    public static string Normalize(string providerName) =>
        providerName switch
        {
            Imdb => Tmdb,
            _ => providerName
        };
}
