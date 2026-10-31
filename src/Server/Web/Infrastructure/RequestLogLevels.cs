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
        "/rest/download"
    ];

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
