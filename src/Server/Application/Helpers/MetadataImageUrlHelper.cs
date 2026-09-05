using K7.Server.Application.Common;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Helpers;

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
        var url = PreferHttps(image.Url);
        if (url is null || !TryCreateRemoteUri(url, out _))
            return null;

        var thumbnailUrl = PreferHttps(image.ThumbnailUrl);
        thumbnailUrl = BuildWikimediaThumbnailUrl(thumbnailUrl) ?? url;

        return image with
        {
            Url = url,
            ThumbnailUrl = thumbnailUrl
        };
    }

    /// <summary>
    /// Cover Art Archive JSON still emits http:// URLs; browsers on HTTPS pages need https for CSP and mixed content.
    /// </summary>
    public static string? PreferHttps(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        if (!IsHttpsUpgradeableImageHost(uri.Host))
            return url;

        var builder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 };
        return builder.Uri.AbsoluteUri;
    }

    private static bool IsHttpsUpgradeableImageHost(string host)
    {
        if (host.Equals("coverartarchive.org", StringComparison.OrdinalIgnoreCase)
            || host.Equals("www.coverartarchive.org", StringComparison.OrdinalIgnoreCase)
            || host.Equals("archive.org", StringComparison.OrdinalIgnoreCase)
            || host.Equals("www.archive.org", StringComparison.OrdinalIgnoreCase))
            return true;

        return host.EndsWith(".archive.org", StringComparison.OrdinalIgnoreCase);
    }

    public static bool MeetsHdStillThreshold(int width, int height) =>
        MetadataPictureThresholds.MeetsHdStillThreshold(width, height);

    public static bool MeetsHdStillThreshold(ProviderImageDto image) =>
        image.Width <= 0 && image.Height <= 0
        || MeetsHdStillThreshold(image.Width, image.Height);

    /// <summary>
    /// Auto numbering often keeps a TVDB screencap. Prefer a later HD still (typically TMDb)
    /// when the current image is below 1280x720, or is a TVDB remote with unknown size.
    /// Keep TMDb / local / source stills until dimensions prove they are not HD.
    /// </summary>
    public static bool ShouldReplaceEpisodeStillWithHdAlternate(
        int? originalWidth,
        int? originalHeight,
        Uri? originalRemoteUri)
    {
        if (originalWidth is > 0 || originalHeight is > 0)
            return !MeetsHdStillThreshold(originalWidth ?? 0, originalHeight ?? 0);

        return string.Equals(
            MetadataProviderHostMapper.FromUri(originalRemoteUri),
            MetadataProviderNames.Tvdb,
            StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ProviderImageDto> FilterProviderImages(IEnumerable<ProviderImageDto> images) =>
        images
            .Select(NormalizeProviderImage)
            .Where(image => image is not null)
            .Cast<ProviderImageDto>()
            .GroupBy(image => image.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(image => image.VoteAverage)
                .ThenByDescending(image => image.Width * image.Height)
                .First())
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
