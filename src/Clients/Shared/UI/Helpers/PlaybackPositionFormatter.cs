namespace K7.Clients.Shared.UI.Helpers;

public static class PlaybackPositionFormatter
{
    /// <summary>
    /// Compact resume position for play buttons (e.g. 1h03, 12min, 45s).
    /// </summary>
    public static string? TryFormat(double seconds)
    {
        if (seconds <= 0)
            return null;

        var totalSeconds = (int)Math.Floor(seconds);
        var totalMinutes = totalSeconds / 60;
        if (totalMinutes >= 60)
        {
            var hours = totalMinutes / 60;
            var mins = totalMinutes % 60;
            return mins > 0 ? $"{hours}h{mins:00}" : $"{hours}h";
        }

        if (totalMinutes > 0)
            return $"{totalMinutes}min";

        return $"{totalSeconds}s";
    }
}
