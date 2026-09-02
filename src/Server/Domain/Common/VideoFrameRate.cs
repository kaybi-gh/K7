namespace K7.Server.Domain.Common;

/// <summary>
/// Plausible content fps from ffprobe. Prefer avg_frame_rate: r_frame_rate is
/// sometimes a timebase (90000) that is not a display rate.
/// </summary>
public static class VideoFrameRate
{
    public static bool IsPlausible(double fps) => fps > 1d && fps < 125d;

    public static bool IsMissing(float? fps) =>
        fps is not float value || !IsPlausible(value);

    public static float? FromProbe(double avgFrameRate, double frameRate)
    {
        if (IsPlausible(avgFrameRate))
            return (float)avgFrameRate;
        if (IsPlausible(frameRate))
            return (float)frameRate;
        return null;
    }

    public static float? FromRateStrings(string? avgFrameRate, string? frameRate) =>
        FromProbe(ParseRate(avgFrameRate), ParseRate(frameRate));

    public static double ParseRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "N/A" || value == "0/0")
            return 0;

        var slash = value.IndexOf('/');
        if (slash < 0)
            return double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var plain)
                ? plain
                : 0;

        var numText = value[..slash];
        var denText = value[(slash + 1)..];
        if (!double.TryParse(numText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var num)
            || !double.TryParse(denText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var den)
            || den == 0)
        {
            return 0;
        }

        return num / den;
    }
}
