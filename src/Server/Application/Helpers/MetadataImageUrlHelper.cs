using K7.Server.Domain.Enums;
using K7.Shared.Helpers;
using K7.Shared.Dtos.Entities.Metadatas;

namespace K7.Server.Application.Helpers;

public static class MetadataImageUrlHelper
{
    public const int CommonsThumbnailWidth = 300;

    private static readonly HashSet<string> VectorExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".svg"
    };

    public static bool IsVectorImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && VectorExtensions.Contains(GetPathExtension(uri));
    }

    public static bool IsVectorFilePath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && VectorExtensions.Contains(Path.GetExtension(path));

    public static string? BuildWikimediaCommonsImageUrl(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return null;

        return $"https://commons.wikimedia.org/wiki/Special:FilePath/{Uri.EscapeDataString(filename)}";
    }

    public static string? BuildWikimediaThumbnailUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        if (!IsWikimediaHost(uri.Host) || !VectorExtensions.Contains(GetPathExtension(uri)))
            return url;

        return EnsureWidthParameter(url.Split('?', 2)[0], CommonsThumbnailWidth);
    }

    public static bool TryCreateRemoteUri(string? url, out Uri? remoteUri)
    {
        remoteUri = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out remoteUri);
    }

    public static ProviderImageDto? NormalizeProviderImage(ProviderImageDto image)
    {
        if (!TryCreateRemoteUri(image.Url, out _))
            return null;

        var thumbnailUrl = BuildWikimediaThumbnailUrl(image.ThumbnailUrl) ?? image.Url;

        return image with
        {
            ThumbnailUrl = thumbnailUrl
        };
    }

    public static bool MeetsHdStillThreshold(int width, int height) =>
        MetadataPictureThresholds.MeetsHdStillThreshold(width, height);

    public static bool MeetsHdStillThreshold(ProviderImageDto image) =>
        image.Width <= 0 && image.Height <= 0
        || MeetsHdStillThreshold(image.Width, image.Height);

    public static IReadOnlyList<ProviderImageDto> FilterProviderImages(IEnumerable<ProviderImageDto> images) =>
        images
            .Select(NormalizeProviderImage)
            .Where(image => image is not null)
            .Cast<ProviderImageDto>()
            .ToList();

    public static IReadOnlyList<ProviderImageDto> FilterHdEpisodeStills(IEnumerable<ProviderImageDto> images) =>
        FilterProviderImages(images)
            .Where(image => image.Type != MetadataPictureType.Still || MeetsHdStillThreshold(image))
            .OrderByDescending(image => image.Type == MetadataPictureType.Still ? image.Width : 0)
            .ThenByDescending(image => image.VoteAverage)
            .ToList();

    public static string? GetExtensionFromContentType(string? contentType)
        => MimeTypeHelper.GetImageExtension(contentType);

    public static bool IsVectorContentType(string? contentType) =>
        string.Equals(
            contentType?.Split(';', 2)[0].Trim(),
            "image/svg+xml",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsWikimediaHost(string host) =>
        host.Equals("commons.wikimedia.org", StringComparison.OrdinalIgnoreCase)
        || host.Equals("upload.wikimedia.org", StringComparison.OrdinalIgnoreCase);

    private static string GetPathExtension(Uri uri)
    {
        var path = uri.AbsolutePath;
        var dotIndex = path.LastIndexOf('.');
        return dotIndex < 0 ? string.Empty : path[dotIndex..];
    }

    private static string EnsureWidthParameter(string url, int width)
    {
        if (url.Contains("width=", StringComparison.OrdinalIgnoreCase))
            return url;

        return url.Contains('?', StringComparison.Ordinal)
            ? $"{url}&width={width}"
            : $"{url}?width={width}";
    }
}
