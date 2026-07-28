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
        return $"{url}{separator}v={version.Value.ToUnixTimeMilliseconds()}";
    }

    /// <summary>
    /// Compares image URLs ignoring cache-buster query strings so refreshes do not treat the same asset as new.
    /// </summary>
    public static bool SameResourceUrl(string? left, string? right)
    {
        if (ReferenceEquals(left, right) || left == right)
            return true;

        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right);

        return StripQuery(left) == StripQuery(right);
    }

    private static string StripQuery(string url)
    {
        var index = url.IndexOf('?', StringComparison.Ordinal);
        return index < 0 ? url : url[..index];
    }
}
