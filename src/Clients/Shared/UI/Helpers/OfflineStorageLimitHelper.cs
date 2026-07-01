namespace K7.Clients.Shared.UI.Helpers;

public static class OfflineStorageLimitHelper
{
    public static long GetEffectiveMaxBytes(long? deviceTotalBytes, long siblingBytes, long configuredMaxBytes, long minBytes)
    {
        var max = configuredMaxBytes;
        if (deviceTotalBytes is { } total)
            max = Math.Min(max, Math.Max(minBytes, total - siblingBytes));

        return Math.Max(minBytes, max);
    }

    public static void ClampLimits(
        ref long maxDownloadBytes,
        ref long maxCacheBytes,
        long? deviceTotalBytes,
        long minDownloadBytes,
        long minCacheBytes)
    {
        if (deviceTotalBytes is not { } total)
            return;

        maxDownloadBytes = Math.Min(maxDownloadBytes, total);
        maxCacheBytes = Math.Min(maxCacheBytes, total);

        if (maxDownloadBytes + maxCacheBytes <= total)
            return;

        if (maxCacheBytes > total - maxDownloadBytes)
            maxCacheBytes = Math.Max(minCacheBytes, total - maxDownloadBytes);

        if (maxDownloadBytes > total - maxCacheBytes)
            maxDownloadBytes = Math.Max(minDownloadBytes, total - maxCacheBytes);
    }
}
