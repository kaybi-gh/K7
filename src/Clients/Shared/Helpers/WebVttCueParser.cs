using System.Globalization;
using System.Text;

namespace K7.Clients.Shared.Helpers;

public readonly record struct WebVttCue(double StartSeconds, double EndSeconds, string Text);

/// <summary>
/// Parses a full WebVTT document into timed cues for the native Android overlay.
/// </summary>
public static class WebVttCueParser
{
    public static IReadOnlyList<WebVttCue> Parse(string? vtt)
    {
        if (string.IsNullOrWhiteSpace(vtt))
            return [];

        var normalized = vtt.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var cues = new List<WebVttCue>();
        var i = 0;

        while (i < lines.Length)
        {
            if (lines[i].Contains("-->", StringComparison.Ordinal))
                break;
            i++;
        }

        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (!line.Contains("-->", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            var (start, end) = ParseCueTiming(line);
            i++;
            var textLines = new List<string>();
            while (i < lines.Length && lines[i].Trim().Length > 0)
            {
                var text = StripCueMarkup(lines[i].TrimEnd());
                if (text.Length > 0)
                    textLines.Add(text);
                i++;
            }

            var body = string.Join('\n', textLines);
            if (end > start && body.Length > 0)
                cues.Add(new WebVttCue(start, end, body));
        }

        return cues;
    }

    public static string? CueAt(IReadOnlyList<WebVttCue> cues, double timeSeconds)
    {
        if (cues.Count == 0)
            return null;

        for (var i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            if (timeSeconds >= cue.StartSeconds && timeSeconds < cue.EndSeconds)
                return cue.Text;
        }

        return null;
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
        catch (FormatException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }

    private static string StripCueMarkup(string line)
    {
        if (line.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(line.Length);
        var inTag = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '<')
            {
                inTag = true;
                continue;
            }

            if (c == '>')
            {
                inTag = false;
                continue;
            }

            if (!inTag)
                sb.Append(c);
        }

        return sb.ToString()
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
