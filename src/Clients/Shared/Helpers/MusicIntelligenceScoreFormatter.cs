namespace K7.Clients.Shared.Helpers;

public static class MusicIntelligenceScoreFormatter
{
    /// <summary>
    /// Formats AudioMuse scores for compact UI display.
    /// Distance/divergence: lower is closer → closeness %. Similarity: higher is better → %.
    /// </summary>
    public static string? Format(double? score, string? metric)
    {
        if (score is not { } value || double.IsNaN(value) || double.IsInfinity(value))
            return null;

        if (IsLowerBetter(metric))
        {
            var closeness = Math.Clamp((1d - value) * 100d, 0d, 100d);
            return $"{closeness:0}%";
        }

        if (string.Equals(metric, "similarity", StringComparison.OrdinalIgnoreCase))
        {
            if (value is >= 0 and <= 1)
                return $"{Math.Clamp(value * 100d, 0d, 100d):0}%";
            return value.ToString("0.00");
        }

        return value.ToString("0.00");
    }

    private static bool IsLowerBetter(string? metric) =>
        string.Equals(metric, "distance", StringComparison.OrdinalIgnoreCase)
        || string.Equals(metric, "divergence", StringComparison.OrdinalIgnoreCase);
}
