using System.Globalization;
using System.Text.RegularExpressions;
using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

public sealed record MpcPlayerVariables(
    string? FilePath,
    PlaybackState State,
    double PositionSeconds,
    double DurationSeconds);

public static class MpcVariablesParser
{
    public static MpcPlayerVariables? TryParse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var positionMs = ReadLong(html, "position");
        var durationMs = ReadLong(html, "duration");
        var stateString = ReadRaw(html, "statestring");
        var stateCode = ReadRaw(html, "state");
        if (positionMs is null && durationMs is null && stateString is null && stateCode is null)
            return null;

        return new MpcPlayerVariables(
            FilePath: ReadRaw(html, "filepath"),
            State: MapState(stateString, stateCode),
            PositionSeconds: (positionMs ?? 0) / 1000.0,
            DurationSeconds: (durationMs ?? 0) / 1000.0);
    }

    private static PlaybackState MapState(string? stateString, string? stateCode)
    {
        if (!string.IsNullOrWhiteSpace(stateString))
        {
            if (stateString.Contains("play", StringComparison.OrdinalIgnoreCase))
                return PlaybackState.Playing;
            if (stateString.Contains("pause", StringComparison.OrdinalIgnoreCase))
                return PlaybackState.Paused;
            if (stateString.Contains("stop", StringComparison.OrdinalIgnoreCase)
                || stateString.Contains("close", StringComparison.OrdinalIgnoreCase))
                return PlaybackState.Ended;
        }

        return stateCode?.Trim() switch
        {
            "2" => PlaybackState.Playing,
            "1" => PlaybackState.Paused,
            "0" or "-1" => PlaybackState.Ended,
            _ => PlaybackState.Unknown
        };
    }

    private static long? ReadLong(string html, string id)
    {
        var raw = ReadRaw(html, id);
        if (raw is null)
            return null;

        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? ReadRaw(string html, string id)
    {
        var dedicated = new Regex(
            $@"<(?:p|span|div)\s+id=""{Regex.Escape(id)}""[^>]*>(.*?)</(?:p|span|div)>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline).Match(html);
        if (!dedicated.Success)
            return null;

        var text = dedicated.Groups[1].Value.Trim();
        return text.Length == 0 ? null : text;
    }
}
