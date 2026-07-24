namespace K7.Clients.Shared.Models;

public static class HarmonicMixHelper
{
    private const float SilencePeakThreshold = 0.08f;
    private const double MinFadeSeconds = 1.0;
    private const double MaxFadeSeconds = 12.0;

    /// <summary>
    /// Computes an adaptive crossfade duration (in seconds) between two tracks.
    /// - MixRamp FadeOut/FadeIn when available
    /// - Else the configured base duration (waveform silence must not shorten the blend)
    /// Returns 0 if crossfade should be skipped (e.g. same album -> gapless).
    /// </summary>
    public static double ComputeCrossfadeDuration(AudioQueueItem current, AudioQueueItem next, double baseDuration = 6.0)
    {
        if (baseDuration <= 0) return 0;

        // Same album -> no crossfade (gapless playback)
        if (current.AlbumTitle is not null && current.AlbumTitle == next.AlbumTitle)
            return 0;

        // Sweet fades (MixRamp): use analyzed fade points when both tracks have data
        if (current.FadeOutDuration.HasValue && next.FadeInDuration.HasValue)
        {
            var overlap = Math.Min(current.FadeOutDuration.Value, next.FadeInDuration.Value);
            return Math.Clamp(overlap, MinFadeSeconds, MaxFadeSeconds);
        }

        // Do not shrink the user-configured overlap from trailing/leading silence:
        // that reads as "stopped early / started early" instead of a musical blend.
        // Waveform silence can only lengthen slightly toward the configured base.
        var fromWaveform = EstimateFadeFromWaveforms(current, next, baseDuration);
        if (fromWaveform is { } fade)
            return Math.Clamp(Math.Max(fade, baseDuration), MinFadeSeconds, MaxFadeSeconds);

        return Math.Clamp(baseDuration, MinFadeSeconds, MaxFadeSeconds);
    }

    private static double? EstimateFadeFromWaveforms(AudioQueueItem current, AudioQueueItem next, double baseDuration)
    {
        var outSeconds = EstimateTrailingSilenceSeconds(current.WaveformPeaks, current.Duration);
        var inSeconds = EstimateLeadingSilenceSeconds(next.WaveformPeaks, next.Duration);

        if (outSeconds is null && inSeconds is null)
            return null;

        var estimated = Math.Max(outSeconds ?? 0, inSeconds ?? 0);
        if (estimated < MinFadeSeconds)
            estimated = MinFadeSeconds;

        // Cap by user base duration so adaptive never exceeds the slider max intent.
        return Math.Clamp(Math.Min(estimated, baseDuration), MinFadeSeconds, MaxFadeSeconds);
    }

    private static double? EstimateTrailingSilenceSeconds(float[]? peaks, double? durationSeconds)
    {
        if (peaks is not { Length: > 8 } || durationSeconds is not > 0)
            return null;

        var silent = 0;
        for (var i = peaks.Length - 1; i >= 0; i--)
        {
            if (peaks[i] > SilencePeakThreshold)
                break;
            silent++;
        }

        if (silent == 0)
            return null;

        return durationSeconds.Value * silent / peaks.Length;
    }

    private static double? EstimateLeadingSilenceSeconds(float[]? peaks, double? durationSeconds)
    {
        if (peaks is not { Length: > 8 } || durationSeconds is not > 0)
            return null;

        var silent = 0;
        for (var i = 0; i < peaks.Length; i++)
        {
            if (peaks[i] > SilencePeakThreshold)
                break;
            silent++;
        }

        if (silent == 0)
            return null;

        return durationSeconds.Value * silent / peaks.Length;
    }
}
