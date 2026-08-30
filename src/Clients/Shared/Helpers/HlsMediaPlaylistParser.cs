using System.Globalization;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Reads a demuxed HLS media playlist (MAP + EXTINF segments) so Windows can
/// concatenate fMP4 into a progressive MP4. LibVLC 3 cannot demux audio-only HLS.
/// </summary>
public static class HlsMediaPlaylistParser
{
    public readonly record struct Segment(double DurationSeconds, string Url);

    public static bool TryParse(
        string playlistText,
        string playlistUrl,
        out string? mapUrl,
        out IReadOnlyList<Segment> segments)
    {
        mapUrl = null;
        segments = [];
        if (string.IsNullOrWhiteSpace(playlistText) || string.IsNullOrWhiteSpace(playlistUrl))
            return false;

        if (!Uri.TryCreate(playlistUrl, UriKind.Absolute, out var playlistUri))
            return false;

        var parsed = new List<Segment>();
        string? pendingDuration = null;
        using var reader = new StringReader(playlistText);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#EXT-X-MAP:", StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadAttributeUri(line, out var mapRelative)
                    && TryResolve(playlistUri, mapRelative, out var resolvedMap))
                {
                    mapUrl = resolvedMap;
                }

                continue;
            }

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["#EXTINF:".Length..];
                var comma = value.IndexOf(',');
                pendingDuration = comma >= 0 ? value[..comma] : value;
                continue;
            }

            if (line.StartsWith('#') || pendingDuration is null)
                continue;

            if (!TryResolve(playlistUri, line, out var segmentUrl))
            {
                pendingDuration = null;
                continue;
            }

            if (!double.TryParse(
                    pendingDuration,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var duration)
                || duration <= 0)
            {
                pendingDuration = null;
                continue;
            }

            parsed.Add(new Segment(duration, segmentUrl));
            pendingDuration = null;
        }

        if (string.IsNullOrEmpty(mapUrl) || parsed.Count == 0)
            return false;

        segments = parsed;
        return true;
    }

    public static int FirstSegmentIndexAtOrBefore(IReadOnlyList<Segment> segments, double startSeconds)
    {
        if (segments.Count == 0 || startSeconds <= 1)
            return 0;

        var elapsed = 0.0;
        for (var i = 0; i < segments.Count; i++)
        {
            var next = elapsed + segments[i].DurationSeconds;
            if (startSeconds < next - 0.0005)
                return i;

            elapsed = next;
        }

        return segments.Count - 1;
    }

    private static bool TryReadAttributeUri(string line, out string uri)
    {
        uri = "";
        const string marker = "URI=\"";
        var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return false;

        start += marker.Length;
        var end = line.IndexOf('"', start);
        if (end <= start)
            return false;

        uri = line[start..end];
        return uri.Length > 0;
    }

    private static bool TryResolve(Uri playlistUri, string relative, out string resolved)
    {
        resolved = "";
        if (Uri.TryCreate(relative, UriKind.Absolute, out var absolute))
        {
            resolved = absolute.AbsoluteUri;
            return true;
        }

        if (!Uri.TryCreate(playlistUri, relative, out var combined))
            return false;

        resolved = combined.AbsoluteUri;
        return true;
    }
}
