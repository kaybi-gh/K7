namespace K7.Clients.Shared.Helpers;

public static class MediaPictureUrlHelper
{
    public static string? WithCacheBuster(string? url, DateTimeOffset? version)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        if (version is null)
            return url;

        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}v={version.Value.ToUnixTimeSeconds()}";
    }
}
