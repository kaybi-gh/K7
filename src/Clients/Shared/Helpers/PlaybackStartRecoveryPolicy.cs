namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Cold remux HLS often 503s <c>init.m4s</c>. Video.js maps that to MEDIA_ERR_SRC_NOT_SUPPORTED
/// (code 4) within a second. That is not remux failed - stay on remux before the encode ladder.
/// Do not reload the HLS source: reloads flip play/pause and can leave the overlay on Play
/// while media is running.
/// </summary>
public static class PlaybackStartRecoveryPolicy
{
    public const int RemuxReloadAttemptsBeforeTranscode = 2;

    public static readonly TimeSpan MinTranscodeFallbackInterval = TimeSpan.FromSeconds(25);

    public static bool ShouldStayOnRemux(
        bool isOriginalQuality,
        bool isHls,
        int remuxReloadsDone,
        DateTime lastRemuxRetryUtc,
        DateTime utcNow)
    {
        _ = remuxReloadsDone;
        if (!isOriginalQuality || !isHls)
            return false;

        if (lastRemuxRetryUtc == DateTime.MinValue)
            return true;

        return utcNow - lastRemuxRetryUtc < MinTranscodeFallbackInterval;
    }

    public static bool ShouldReloadRemuxSource(int remuxReloadsDone)
    {
        _ = remuxReloadsDone;
        return false;
    }
}
