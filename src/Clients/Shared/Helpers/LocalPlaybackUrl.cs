using System.Diagnostics.CodeAnalysis;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Local/offline media paths vs HTTP(S) stream URLs. Android/Windows LibVLC opens files via
/// <c>FromPath</c>. iOS MediaElement uses <c>FromFile</c>.
/// </summary>
public static class LocalPlaybackUrl
{
    public static bool IsLocalFile(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (HasHttpScheme(url))
            return false;

        if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return true;

        return Path.IsPathRooted(StripQuery(url));
    }

    public static bool HasHttpScheme(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    public static bool TryGetLocalFilesystemPath(string? url, [NotNullWhen(true)] out string? path)
    {
        if (!IsLocalFile(url))
        {
            path = null;
            return false;
        }

        path = ToFilesystemPath(url!);
        return true;
    }

    public static string ToFilesystemPath(string url)
    {
        var withoutQuery = StripQuery(url);
        if (withoutQuery.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(withoutQuery, UriKind.Absolute, out var uri)
            && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return withoutQuery;
    }

    /// <summary>
    /// Builds an absolute <c>file://</c> URI. <c>new Uri(unixPath)</c> throws
    /// <see cref="UriFormatException"/> because Android paths have no scheme.
    /// </summary>
    public static Uri CreateFileUri(string localPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        var fullPath = Path.GetFullPath(localPath);
        var slashPath = fullPath.Replace('\\', '/');
        if (!slashPath.StartsWith('/'))
            slashPath = "/" + slashPath;
        return new Uri("file://" + slashPath);
    }

    private static string StripQuery(string url)
    {
        var query = url.IndexOf('?');
        return query >= 0 ? url[..query] : url;
    }
}
