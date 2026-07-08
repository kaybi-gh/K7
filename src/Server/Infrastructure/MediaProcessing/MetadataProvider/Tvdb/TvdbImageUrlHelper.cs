namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

internal static class TvdbImageUrlHelper
{
    private const string ArtworkHostUrl = "https://artworks.thetvdb.com/";
    private const string ArtworkBannersUrl = "https://artworks.thetvdb.com/banners/";

    public static string? BuildImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

        if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return imagePath;

        var normalized = imagePath.TrimStart('/');

        if (normalized.StartsWith("banners/", StringComparison.OrdinalIgnoreCase))
            return $"{ArtworkHostUrl}{normalized}";

        return $"{ArtworkBannersUrl}{normalized}";
    }
}
