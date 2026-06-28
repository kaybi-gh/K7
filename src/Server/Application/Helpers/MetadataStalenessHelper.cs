namespace K7.Server.Application.Helpers;

public static class MetadataStalenessHelper
{
    public static bool HasAutoRefresh(int? metadataRefreshIntervalDays) =>
        metadataRefreshIntervalDays is > 0;

    public static DateTimeOffset? GetStalenessThresholdUtc(int? metadataRefreshIntervalDays, DateTimeOffset utcNow) =>
        HasAutoRefresh(metadataRefreshIntervalDays)
            ? utcNow.AddDays(-metadataRefreshIntervalDays!.Value)
            : null;

    public static bool IsStale(DateTimeOffset? lastMetadataRefreshedAt, int? metadataRefreshIntervalDays, DateTimeOffset utcNow)
    {
        var threshold = GetStalenessThresholdUtc(metadataRefreshIntervalDays, utcNow);
        if (threshold is null)
            return false;

        return lastMetadataRefreshedAt is null || lastMetadataRefreshedAt < threshold;
    }
}
