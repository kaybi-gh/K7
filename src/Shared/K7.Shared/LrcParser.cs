using System.Globalization;
using System.Text.RegularExpressions;

namespace K7.Shared;

public sealed record LrcLine(TimeSpan Timestamp, string Text);

public static partial class LrcParser
{
    [GeneratedRegex(@"\[(\d{1,3}):(\d{2})(?:[.:])(\d{2,3})\]", RegexOptions.Compiled)]
    private static partial Regex TimestampRegex();

    public static List<LrcLine> Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
            return [];

        var lines = new List<LrcLine>();

        foreach (var rawLine in lrc.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var matches = TimestampRegex().Matches(line);
            if (matches.Count == 0) continue;

            var text = TimestampRegex().Replace(line, "").Trim();

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var fractional = match.Groups[3].Value;

                var milliseconds = fractional.Length == 2
                    ? int.Parse(fractional, CultureInfo.InvariantCulture) * 10
                    : int.Parse(fractional, CultureInfo.InvariantCulture);

                var ts = new TimeSpan(0, 0, minutes, seconds, milliseconds);
                lines.Add(new LrcLine(ts, text));
            }
        }

        lines.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return lines;
    }
}
