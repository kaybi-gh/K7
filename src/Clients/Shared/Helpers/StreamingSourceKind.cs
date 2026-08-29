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
    public static bool TryBuildHlsManifestUrl(
        string? currentUrl,
        Guid? streamSessionId,
        out string hlsUrl)
    {
        hlsUrl = "";
        if (string.IsNullOrWhiteSpace(currentUrl) || streamSessionId is null)
            return false;

        if (IsHls(mimeType: null, currentUrl))
        {
            hlsUrl = currentUrl;
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
        return true;
    }
}
