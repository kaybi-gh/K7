namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Distinguishes demuxed HLS (reload the master to change audio) from a muxed
/// file (direct-stream or local), where the player switches tracks in-container.
/// </summary>
public static class StreamingSourceKind
{
    public static bool IsHls(string? mimeType, string? url)
    {
        if (mimeType is not null
            && mimeType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(url) || LocalPlaybackUrl.IsLocalFile(url))
            return false;

        return url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/hls-stream/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Direct-stream URLs have no Quality / burn-in query. Build the HLS master for the
    /// same file and session so the client can leave Original without a new session.
    /// </summary>
    /// <param name="videoJsCompatible">
    /// When true, appends <c>VideoCodecsOnly=true</c> so MSE does not see combined
    /// video+ec-3 CODECS (Windows Video.js after Direct Play promote).
    /// </param>
    public static bool TryBuildHlsManifestUrl(
        string? currentUrl,
        Guid? streamSessionId,
        out string hlsUrl,
        bool videoJsCompatible = false)
    {
        hlsUrl = "";
        if (string.IsNullOrWhiteSpace(currentUrl) || streamSessionId is null)
            return false;

        if (IsHls(mimeType: null, currentUrl))
        {
            hlsUrl = videoJsCompatible
                ? EnsureVideoJsHlsManifestQuery(currentUrl)
                : currentUrl;
            return true;
        }

        var marker = "/direct-stream";
        var index = currentUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        hlsUrl = string.Concat(
            currentUrl.AsSpan(0, index),
            "/hls-stream/manifest.m3u8?StreamSessionId=",
            streamSessionId.Value.ToString("D"));

        if (videoJsCompatible)
            hlsUrl = EnsureVideoJsHlsManifestQuery(hlsUrl);

        return true;
    }

    /// <summary>
    /// Ensures Video.js / VHS master requests use video-only CODECS (MSE isTypeSupported).
    /// </summary>
    public static string EnsureVideoJsHlsManifestQuery(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (url.Contains("VideoCodecsOnly=", StringComparison.OrdinalIgnoreCase))
            return url;

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return url + separator + "VideoCodecsOnly=true";
    }

    /// <summary>
    /// Inverse of <see cref="TryBuildHlsManifestUrl"/> so Original can leave a promoted
    /// HLS session and return to muxed <c>/direct-stream</c>.
    /// </summary>
    public static bool TryBuildDirectStreamUrl(string? currentUrl, out string directUrl)
    {
        directUrl = "";
        if (string.IsNullOrWhiteSpace(currentUrl))
            return false;

        if (currentUrl.Contains("/direct-stream", StringComparison.OrdinalIgnoreCase)
            && !IsHls(mimeType: null, currentUrl))
        {
            directUrl = currentUrl;
            return true;
        }

        var marker = "/hls-stream/";
        var index = currentUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        directUrl = string.Concat(currentUrl.AsSpan(0, index), "/direct-stream");
        return true;
    }
}
