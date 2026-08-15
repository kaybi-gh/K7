using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

public readonly record struct DownloadKeepAliveSnapshot(int ActiveCount, DownloadQueueItem? Current);

public static class DownloadQueueKeepAlive
{
    public static bool IsActiveStatus(DownloadItemStatus status) =>
        status is DownloadItemStatus.Queued or DownloadItemStatus.Preparing or DownloadItemStatus.Downloading;

    public static bool RequiresKeepAlive(IEnumerable<DownloadQueueItem> queue)
    {
        foreach (var item in queue)
        {
            if (item.Request.IsCacheItem)
                continue;
            if (IsActiveStatus(item.Status))
                return true;
        }

        return false;
    }

    public static DownloadKeepAliveSnapshot CreateSnapshot(IEnumerable<DownloadQueueItem> queue)
    {
        DownloadQueueItem? current = null;
        var currentRank = -1;
        var activeCount = 0;

        foreach (var item in queue)
        {
            if (item.Request.IsCacheItem || !IsActiveStatus(item.Status))
                continue;

            activeCount++;
            var rank = Rank(item.Status);
            if (rank > currentRank)
            {
                current = item;
                currentRank = rank;
            }
        }

        return new DownloadKeepAliveSnapshot(activeCount, current);
    }

    private static int Rank(DownloadItemStatus status) => status switch
    {
        DownloadItemStatus.Downloading => 2,
        DownloadItemStatus.Preparing => 1,
        _ => 0
    };
}
