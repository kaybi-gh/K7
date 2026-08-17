using System.Globalization;

namespace K7.Server.Domain.Helpers;

/// <summary>
/// Parses ffprobe CSV keyframe lines. Packet output is pts[,dts],flags;
/// skip_frame nokey frame output is pts[,dts] with every line a keyframe.
/// </summary>
public static class HlsKeyframeTimestampParser
{
    public static bool TryParsePacketLine(string line, out long timestampMs)
    {
        timestampMs = 0;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var parts = line.Split(',');
        if (parts.Length < 2)
            return false;

        var flags = parts[^1];
        if (flags.IndexOf('K', StringComparison.Ordinal) < 0)
            return false;

        return TryParseFirstTimestampMs(parts, out timestampMs);
    }

    public static bool TryParseKeyframeFrameLine(string line, out long timestampMs)
    {
        timestampMs = 0;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return TryParseFirstTimestampMs(line.Split(','), out timestampMs);
    }

    private static bool TryParseFirstTimestampMs(string[] parts, out long timestampMs)
    {
        timestampMs = 0;
        var fieldCount = parts.Length > 0 && LooksLikeFlags(parts[^1])
            ? parts.Length - 1
            : parts.Length;

        for (var i = 0; i < fieldCount; i++)
        {
            if (TryParseTimestampMs(parts[i], out timestampMs))
                return true;
        }

        return false;
    }

    private static bool LooksLikeFlags(string value) =>
        value.Length > 0
        && !char.IsDigit(value[0])
        && value[0] != '-'
        && !value.Equals("N/A", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseTimestampMs(string value, out long timestampMs)
    {
        timestampMs = 0;
        if (string.IsNullOrWhiteSpace(value)
            || value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return false;

        timestampMs = (long)(seconds * 1000);
        return true;
    }
}
