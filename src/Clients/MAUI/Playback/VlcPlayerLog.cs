namespace K7.Clients.MAUI.Playback;

/// <summary>
/// LibVLC Direct Play helpers. Logging hooks are intentionally no-ops.
/// </summary>
internal static class VlcPlayerLog
{
    public static void Info(string message) => _ = message;

    public static void Warn(string message) => _ = message;

    public static bool IsExpectedClientDisconnect(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message ?? "";
            if (message.Contains("Broken pipe", StringComparison.OrdinalIgnoreCase)
                || message.Contains("net_io_writefailure", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Connection reset", StringComparison.OrdinalIgnoreCase)
                || message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Software caused connection abort", StringComparison.OrdinalIgnoreCase)
                || message.Contains("remote host", StringComparison.OrdinalIgnoreCase)
                || (message.Contains("distant", StringComparison.OrdinalIgnoreCase)
                    && message.Contains("connexion", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public static string SummarizeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return "-";

        var path = url;
        var query = path.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
            path = path[..query];

        if (path.Contains("/direct-stream", StringComparison.OrdinalIgnoreCase))
            return "direct-stream";
        if (path.Contains("/audio-fmp4", StringComparison.OrdinalIgnoreCase))
            return "audio-fmp4";
        if (path.Contains("/audio-master.m3u8", StringComparison.OrdinalIgnoreCase))
            return "audio-master";
        if (path.Contains("/hls-stream", StringComparison.OrdinalIgnoreCase))
            return "hls-stream";
        if (path.Contains("file:", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith('/')
            || path.Contains("/data/", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(path))
        {
            return "local-file";
        }

        return path.Length > 48 ? "..." + path[^40..] : path;
    }
}
