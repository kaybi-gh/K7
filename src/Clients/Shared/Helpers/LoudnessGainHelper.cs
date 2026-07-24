namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Shared ReplayGain / LUFS gain math used by Web (JS mirror) and native MAUI players.
/// </summary>
public static class LoudnessGainHelper
{
    public static double ComputeLinearGain(
        bool enabled,
        double targetLufs,
        double preampDb,
        double? trackLoudnessLufs,
        double? replayGainTrackGainDb)
    {
        if (!enabled)
            return 1.0;

        double gainDb;
        if (trackLoudnessLufs is { } lufs)
            gainDb = targetLufs - lufs + preampDb;
        else if (replayGainTrackGainDb is { } rg)
            gainDb = rg + preampDb;
        else
            return 1.0;

        gainDb = Math.Clamp(gainDb, -20.0, 20.0);
        return Math.Pow(10.0, gainDb / 20.0);
    }

    /// <summary>
    /// Soft ceiling used when a hard brick-wall limiter is not available on the platform.
    /// </summary>
    public static double ApplySoftLimiter(double linearGain, bool limiterEnabled)
    {
        if (!limiterEnabled)
            return linearGain;

        // Cap around -1 dBFS equivalent (~0.89) so peaks rarely clip on volume-only paths.
        return Math.Min(linearGain, 0.89);
    }
}
