namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// When a remux head should stop because the next shared segment is already ready.
/// </summary>
internal static class RemuxHeadOverlapStopPolicy
{
    /// <summary>
    /// Stop once this head has a landing and the next shared index is already
    /// cached, but never while shared <c>init.m4s</c> is still missing.
    /// Resume mid-movie starts ffmpeg at current-5 on a window that is already
    /// on disk. Without the init guard the promote loop killed the head before
    /// ffmpeg wrote init, then EnsureInit spawned another head (77, 78, 79).
    /// </summary>
    public static bool ShouldStopBecauseNextIsReady(
        int from,
        int tipIndex,
        int untilInclusive,
        bool landingReadyOnShared,
        bool nextReadyOnShared,
        bool nextExistsInStaging,
        bool initReadyOnShared)
    {
        var next = tipIndex + 1;
        if (next > untilInclusive || next <= from)
            return false;

        if (!initReadyOnShared)
            return false;

        return landingReadyOnShared && nextReadyOnShared && !nextExistsInStaging;
    }
}
