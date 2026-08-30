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
            || url.Contains("/remote-stream-sessions/", StringComparison.OrdinalIgnoreCase)
            || (url.Contains("/subtitles/", StringComparison.OrdinalIgnoreCase)
                && url.Contains(".vtt", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Rewrites relative playlist and segment URIs to absolute URLs against the fetched manifest URL.
    /// WebView2 proxied m3u8 responses can break HLS client base-URL resolution for nested playlists.
    /// Also injects TranscodingAudioCodec=aac on demuxed audio URLs when missing: Windows Video.js
    /// cannot remux EAC3/DDP into fMP4 (server returns 503 on init.m4s with a stuck remux job).
    /// </summary>
    public static string AbsolutizeManifestUrls(string manifest, Uri manifestUrl)
    {
        if (string.IsNullOrEmpty(manifest))
            return manifest;

        var lines = manifest.Split('\n');
        var builder = new StringBuilder(manifest.Length + 256);
        var ensureAacOnAudio = IsHlsAudioPlaylistUrl(manifestUrl.AbsoluteUri)
            || manifest.Contains("TYPE=AUDIO", StringComparison.OrdinalIgnoreCase);

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
                    return $"URI=\"{AbsolutizePlaylistUri(uri, manifestUrl, ensureAacOnAudio)}\"";
                }));
            }
            else
            {
                builder.Append(AbsolutizePlaylistUri(line, manifestUrl, ensureAacOnAudio));
            }

            if (i < lines.Length - 1)
                builder.AppendLine();
        }

        if (manifest.EndsWith('\n') || manifest.EndsWith("\r\n"))
            builder.AppendLine();

        return builder.ToString();
    }

    /// <summary>
    /// Ensures demuxed audio HLS requests ask the server for AAC (Video.js / MSE path).
    /// </summary>
    public static string EnsureWindowsHlsAudioTranscodeQuery(string url)
    {
        if (string.IsNullOrEmpty(url) || !IsHlsAudioResourceUrl(url))
            return url;

        if (url.Contains("TranscodingAudioCodec=", StringComparison.OrdinalIgnoreCase))
            return url;

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return url + separator + "TranscodingAudioCodec=aac";
    }

    private static string AbsolutizePlaylistUri(string uri, Uri manifestUrl, bool ensureAacOnAudio)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return uri;

        string absolute;
        if (Uri.TryCreate(uri, UriKind.Absolute, out var absoluteUri))
            absolute = absoluteUri.AbsoluteUri;
        else
            absolute = new Uri(manifestUrl, uri).AbsoluteUri;

        if (!ensureAacOnAudio)
            return absolute;

        // Master EXT-X-MEDIA audio URIs and audio media-playlist segment/init URIs.
        if (IsHlsAudioResourceUrl(absolute)
            || (IsHlsAudioPlaylistUrl(manifestUrl.AbsoluteUri)
                && absolute.Contains("/segments/", StringComparison.OrdinalIgnoreCase)))
        {
            return EnsureWindowsHlsAudioTranscodeQuery(absolute);
        }

        return absolute;
    }

    private static bool IsHlsAudioPlaylistUrl(string url) =>
        url.Contains("/hls-stream/audio/", StringComparison.OrdinalIgnoreCase)
        && url.Contains("index.m3u8", StringComparison.OrdinalIgnoreCase);

    private static bool IsHlsAudioResourceUrl(string url) =>
        url.Contains("/hls-stream/audio/", StringComparison.OrdinalIgnoreCase);
}
#endif
