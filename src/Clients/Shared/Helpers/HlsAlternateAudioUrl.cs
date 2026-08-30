namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Builds the demuxed HLS audio media playlist URL. LibVLC 3 on Windows cannot
/// play that playlist (audio-only fMP4 HLS); the Windows proxy concatenates
/// <c>init.m4s</c> + <c>.m4s</c> into a progressive MP4 instead.
/// </summary>
public static class HlsAlternateAudioUrl
{
    public static string? TryBuildSlaveUrl(
        string playUrl,
        string originalMasterUrl,
        int audioTrackIndex,
        double startSeconds)
    {
        if (audioTrackIndex < 0 || string.IsNullOrWhiteSpace(playUrl))
            return null;

        var sessionId = TryReadQueryValue(originalMasterUrl, "StreamSessionId")
            ?? TryReadQueryValue(originalMasterUrl, "streamSessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var slash = playUrl.LastIndexOf('/');
        if (slash < 0)
            return null;

        var query = "streamSessionId=" + sessionId;
        if (startSeconds > 1)
        {
            query += "&startSeconds="
                + startSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }

        return playUrl[..(slash + 1)]
            + "audio/"
            + audioTrackIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "/index.m3u8?"
            + query;
    }

    public static string? TryReadQueryValue(string url, string key)
    {
        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0 || queryIndex >= url.Length - 1)
            return null;

        foreach (var part in url[(queryIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            if (!part[..eq].Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            return Uri.UnescapeDataString(part[(eq + 1)..]);
        }

        return null;
    }
}
