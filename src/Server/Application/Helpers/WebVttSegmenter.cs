using System.Globalization;
using System.Text;
using K7.Shared.Constants;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Extracts time-range segments from a full WebVTT file.
/// Produces a valid WebVTT segment with X-TIMESTAMP-MAP for HLS sync.
/// </summary>
internal static class WebVttSegmenter
{
    public static string EmptySegment()
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Extracts cues that overlap the given time range and returns a valid WebVTT segment.
    /// </summary>
    public static string ExtractSegment(string fullVttContent, double startTimeSeconds, double endTimeSeconds)
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        AppendCues(sb, fullVttContent, (cueStart, cueEnd) => cueStart < endTimeSeconds && cueEnd > startTimeSeconds);

        return sb.ToString();
    }

    /// <summary>
    /// Joins consecutive extraction windows into one VTT flagged as partial. Cue times are
    /// untouched. Every window also holds the cues that begin shortly before it: the first
    /// one keeps them, the next ones only add the cues beginning in their own range, so a cue
    /// is never written twice.
    /// </summary>
    public static string MergeWindows(IReadOnlyList<string> windowVttContents, double firstWindowStartSeconds, double windowSeconds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();
        sb.AppendLine(SubtitleVttFormat.PartialNote);
        sb.AppendLine();

        for (var i = 0; i < windowVttContents.Count; i++)
        {
            var windowStart = firstWindowStartSeconds + (i * windowSeconds);
            var minCueStart = i == 0 ? double.NegativeInfinity : windowStart;
            var maxCueStart = windowStart + windowSeconds;
            AppendCues(sb, windowVttContents[i], (cueStart, _) => cueStart >= minCueStart && cueStart < maxCueStart);
        }

        return sb.ToString();
    }

    private static void AppendCues(StringBuilder sb, string vttContent, Func<double, double, bool> keepCue)
    {
        var lines = vttContent.Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (line.Contains("-->"))
                break;
            i++;
        }

        while (i < lines.Length)
        {
            var line = lines[i].Trim();

            if (line.Contains("-->"))
            {
                var (cueStart, cueEnd) = ParseCueTiming(line);

                if (keepCue(cueStart, cueEnd))
                {
                    sb.AppendLine(line);
                    i++;

                    while (i < lines.Length && lines[i].Trim().Length > 0)
                    {
                        sb.AppendLine(lines[i].TrimEnd());
                        i++;
                    }

                    sb.AppendLine();
                }
                else
                {
                    i++;
                    while (i < lines.Length && lines[i].Trim().Length > 0)
                        i++;
                }
            }
            else
            {
                i++;
            }
        }
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("WEBVTT");
        // Cue times are on the movie timeline. MPEGTS:0 + LOCAL 0 maps cue T to playlist T.
        sb.AppendLine("X-TIMESTAMP-MAP=MPEGTS:0,LOCAL:00:00:00.000");
        sb.AppendLine();
    }

    private static (double Start, double End) ParseCueTiming(string timingLine)
    {
        var parts = timingLine.Split("-->");
        if (parts.Length != 2)
            return (0, 0);

        var endPart = parts[1].Trim();
        var endSpaceIndex = endPart.IndexOf(' ');
        if (endSpaceIndex > 0)
            endPart = endPart[..endSpaceIndex];

        return (ParseVttTimestamp(parts[0].Trim()), ParseVttTimestamp(endPart));
    }

    private static double ParseVttTimestamp(string timestamp)
    {
        var parts = timestamp.Split(':');
        try
        {
            return parts.Length switch
            {
                3 => double.Parse(parts[0], CultureInfo.InvariantCulture) * 3600
                   + double.Parse(parts[1], CultureInfo.InvariantCulture) * 60
                   + double.Parse(parts[2], CultureInfo.InvariantCulture),
                2 => double.Parse(parts[0], CultureInfo.InvariantCulture) * 60
                   + double.Parse(parts[1], CultureInfo.InvariantCulture),
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }
}
