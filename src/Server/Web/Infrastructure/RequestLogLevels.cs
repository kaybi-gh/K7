namespace K7.Server.Web.Infrastructure;

/// <summary>
/// Request log levels: media segments, Range requests, images and the hub fire many requests
/// per minute and per viewer, so their successful completions log at Debug.
/// </summary>
public static class RequestLogLevels
{
    private static readonly string[] HighVolumePathFragments =
    [
        "/hls-stream/",
        "/direct-stream",
        "/hls/",
        "/hls-segments",
        "/api/remote-stream-sessions/",
        "/api/federation/stream",
        "/api/metadata-pictures/",
        "/rest/getCoverArt",
        "/rest/stream",
        "/rest/download",
        // Sidecar subtitles: clients poll while a track is being extracted.
        "/subtitles/"
    ];

    /// <summary>
    /// A 503 on a WebVTT path means "extraction in progress, ask again", which clients do
    /// every second. It is the normal flow, not a server error: a slow extraction is reported
    /// once by the extractor itself.
    /// </summary>
    public static bool IsSubtitleExtractionInProgress(PathString path, int statusCode) =>
        statusCode == StatusCodes.Status503ServiceUnavailable
        && path.Value is { } value
        && value.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase);

    public static bool IsHighVolumePath(PathString path)
    {
        if (path.StartsWithSegments("/hub"))
            return true;

        var value = path.Value;
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var fragment in HighVolumePathFragments)
        {
            if (value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
