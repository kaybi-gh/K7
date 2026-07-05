namespace K7.Server.Application.Common;

public static class MetadataProviderNames
{
    public static string Normalize(string providerName) =>
        providerName switch
        {
            "imdb" => "tmdb",
            _ => providerName
        };
}
