namespace K7.Server.Web.Infrastructure;

public static class LinkPreviewUrls
{
    public const string ImageAsset = "_content/K7.Clients.Shared.UI/assets/og-image.png";
    public const string IconAsset = "_content/K7.Clients.Shared.UI/assets/icon.png";
    public const int ImageWidth = 1200;
    public const int ImageHeight = 630;

    public static string MetadataPicturePath(Guid id) =>
        $"/api/metadata-pictures/{id}?size=Medium";

    public static string Origin(HttpRequest request, string fallbackUrl)
    {
        if (request.Host.HasValue)
            return $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');

        return string.IsNullOrWhiteSpace(fallbackUrl)
            ? string.Empty
            : fallbackUrl.TrimEnd('/');
    }

    public static string Combine(string origin, string path)
    {
        if (string.IsNullOrEmpty(path))
            return origin;

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return path;

        var relative = path.StartsWith('/') ? path : "/" + path;
        return origin + relative;
    }

    public static string CanonicalPage(string origin, PathString path)
    {
        var value = path.HasValue && path.Value is { Length: > 0 } p ? p : "/";
        return Combine(origin, value);
    }

    public static string Image(string origin, Guid? pictureId, string fallbackPath) =>
        pictureId is { } id
            ? Combine(origin, MetadataPicturePath(id))
            : Combine(origin, fallbackPath);
}
