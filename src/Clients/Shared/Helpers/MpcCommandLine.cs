namespace K7.Clients.Shared.Helpers;

public static class MpcCommandLine
{
    public static int ToStartMilliseconds(double? startPositionSeconds)
    {
        if (startPositionSeconds is not > 0)
            return 0;

        return (int)Math.Round(startPositionSeconds.Value * 1000.0, MidpointRounding.AwayFromZero);
    }

    public static string Build(string mediaUrl, string? extraArgs, int startMilliseconds, int webPort = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaUrl);

        var parts = new List<string>();

        if (webPort is >= 1 and <= 65535 && !ContainsSwitch(extraArgs, "/webport"))
        {
            parts.Add("/webport");
            parts.Add(webPort.ToString());
        }

        if (!string.IsNullOrWhiteSpace(extraArgs))
            parts.Add(extraArgs.Trim());

        if (startMilliseconds > 0 && !ContainsSwitch(extraArgs, "/start"))
        {
            parts.Add("/start");
            parts.Add(startMilliseconds.ToString());
        }

        parts.Add(Quote(mediaUrl));
        return string.Join(' ', parts);
    }

    private static bool ContainsSwitch(string? extraArgs, string switchName)
    {
        if (string.IsNullOrWhiteSpace(extraArgs))
            return false;

        return extraArgs.Contains(switchName, StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value)
    {
        if (value.StartsWith('"') && value.EndsWith('"'))
            return value;

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
