using System.Globalization;
using System.Text;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Builds demuxed HLS media playlists (fMP4) for video or audio variants.
/// </summary>
public static class HlsMediaPlaylistBuilder
{
    public static string Build(
        IReadOnlyList<double> segmentDurationsSeconds,
        string queryString,
        Func<int, string> segmentRelativePathFactory,
        double? startSeconds = null)
    {
        if (segmentDurationsSeconds.Count == 0)
            throw new ArgumentException("At least one segment duration is required.", nameof(segmentDurationsSeconds));

        var content = new StringBuilder();
        content.AppendLine("#EXTM3U");
        content.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
        content.AppendLine($"#EXT-X-TARGETDURATION:{Math.Ceiling(segmentDurationsSeconds.Max())}");
        content.AppendLine("#EXT-X-VERSION:7");
        content.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
        content.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");

        if (startSeconds is > 0)
        {
            var snapped = HlsSegmentHelper.AlignToPreviousSegmentBoundary(
                startSeconds.Value,
                segmentDurationsSeconds);
            var offset = snapped.ToString("F3", CultureInfo.InvariantCulture);
            content.AppendLine($"#EXT-X-START:TIME-OFFSET={offset},PRECISE=NO");
        }

        var qs = string.IsNullOrEmpty(queryString)
            ? string.Empty
            : (queryString.StartsWith("?", StringComparison.Ordinal) ? queryString : "?" + queryString);

        content.AppendLine($"#EXT-X-MAP:URI=\"segments/init.m4s{qs}\"");

        for (var i = 0; i < segmentDurationsSeconds.Count; i++)
        {
            content.AppendLine(
                $"#EXTINF:{segmentDurationsSeconds[i].ToString("F6", CultureInfo.InvariantCulture)},");
            content.AppendLine($"{segmentRelativePathFactory(i)}{qs}");
        }

        content.AppendLine("#EXT-X-ENDLIST");
        return content.ToString();
    }

    public static string BuildQueryString(Guid streamSessionId, params (string Key, string? Value)[] optionalParams)
    {
        var parts = new List<string>
        {
            $"streamSessionId={streamSessionId}"
        };

        foreach (var (key, value) in optionalParams)
        {
            if (!string.IsNullOrEmpty(value))
                parts.Add($"{key}={value}");
        }

        return "?" + string.Join("&", parts);
    }
}
