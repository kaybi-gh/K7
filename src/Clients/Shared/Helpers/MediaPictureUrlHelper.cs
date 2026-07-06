namespace K7.Clients.Shared.Helpers;

public static class MediaPictureUrlHelper
{
    private const string MetadataPicturesPath = "metadata-pictures";

    public static string? WithCacheBuster(string? url, DateTimeOffset? version)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        if (version is null)
            return url;

        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}v={version.Value.ToUnixTimeSeconds()}";
    }

    public static bool IsMetadataPictureUrl(string? url) =>
        !string.IsNullOrEmpty(url)
        && url.Contains(MetadataPicturesPath, StringComparison.OrdinalIgnoreCase);

    public static string WithRetryToken(string url, int attempt)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}_pending={attempt}";
    }
}
