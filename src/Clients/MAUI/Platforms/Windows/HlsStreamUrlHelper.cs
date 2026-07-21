#if WINDOWS
using System.Text;
using System.Text.RegularExpressions;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// HLS URL helpers used by the Windows WebView2 Video.js xhr bridge.
/// </summary>
public static partial class HlsStreamUrlHelper
{
    [GeneratedRegex("URI=\"([^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ManifestUriAttributeRegex();

    /// <summary>
    /// True for K7 indexed-file HLS/direct stream URLs that must not be served by BlazorWebView static interception.
    /// </summary>
    public static bool IsK7StreamResource(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        return url.Contains("/hls-stream/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/direct-stream", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/remote-stream-sessions/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rewrites relative playlist and segment URIs to absolute URLs against the fetched manifest URL.
    /// WebView2 proxied m3u8 responses can break HLS client base-URL resolution for nested playlists.
    /// </summary>
    public static string AbsolutizeManifestUrls(string manifest, Uri manifestUrl)
    {
        if (string.IsNullOrEmpty(manifest))
            return manifest;

        var lines = manifest.Split('\n');
        var builder = new StringBuilder(manifest.Length + 256);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0)
            {
                if (i < lines.Length - 1)
                    builder.AppendLine();

                continue;
            }

            if (line.StartsWith('#'))
            {
                builder.Append(ManifestUriAttributeRegex().Replace(line, match =>
                {
                    var uri = match.Groups[1].Value;
                    return $"URI=\"{AbsolutizePlaylistUri(uri, manifestUrl)}\"";
                }));
            }
            else
            {
                builder.Append(AbsolutizePlaylistUri(line, manifestUrl));
            }

            if (i < lines.Length - 1)
                builder.AppendLine();
        }

        if (manifest.EndsWith('\n') || manifest.EndsWith("\r\n"))
            builder.AppendLine();

        return builder.ToString();
    }

    private static string AbsolutizePlaylistUri(string uri, Uri manifestUrl)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return uri;

        if (Uri.TryCreate(uri, UriKind.Absolute, out _))
            return uri;

        return new Uri(manifestUrl, uri).AbsoluteUri;
    }
}
#endif
