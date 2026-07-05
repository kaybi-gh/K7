namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

internal static class TvdbImageUrlHelper
{
    private const string ArtworkBaseUrl = "https://artworks.thetvdb.com/banners/";

    public static string? BuildImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

        if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return imagePath;

        return $"{ArtworkBaseUrl}{imagePath.TrimStart('/')}";
    }
}
