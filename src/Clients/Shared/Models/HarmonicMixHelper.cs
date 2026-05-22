using K7.Shared;

namespace K7.Clients.Shared.Models;

public static class HarmonicMixHelper
{
    /// <summary>
    /// Computes an adaptive crossfade duration (in seconds) between two tracks.
    /// - When MixRamp data is available (FadeOutDuration/FadeInDuration), uses the overlap point
    /// - Harmonically compatible + similar BPM -> longer crossfade (smooth blend)
    /// - Large energy gap -> shorter crossfade (clean cut)
    /// - No analysis data -> default crossfade
    /// Returns 0 if crossfade should be skipped (e.g. same album, gapless).
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
            return Math.Clamp(overlap, 1.0, 12.0);
        }

        var factor = 1.0;

        // Harmonic compatibility -> blend longer (up to +50%)
        if (CamelotWheel.AreKeysCompatible(current.MusicalKey, next.MusicalKey))
            factor += 0.5;

        // BPM proximity -> blend longer if close (within 5%)
        if (current.Bpm.HasValue && next.Bpm.HasValue)
        {
            var bpmRatio = Math.Abs(current.Bpm.Value - next.Bpm.Value) / Math.Max(current.Bpm.Value, 1);
            if (bpmRatio < 0.05)
                factor += 0.3;
            else if (bpmRatio > 0.2)
                factor -= 0.4; // Large BPM gap -> shorter crossfade
        }

        // Energy gap -> large gap means clean cut
        if (current.Energy.HasValue && next.Energy.HasValue)
        {
            var energyGap = Math.Abs(current.Energy.Value - next.Energy.Value);
            if (energyGap > 0.5)
                factor -= 0.5; // Very different energy -> short crossfade
            else if (energyGap < 0.15)
                factor += 0.2; // Similar energy -> smooth blend
        }

        return Math.Clamp(baseDuration * factor, 1.0, 12.0);
    }
}
