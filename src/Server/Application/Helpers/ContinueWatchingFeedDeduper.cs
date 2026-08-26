namespace K7.Server.Application.Helpers;

internal readonly record struct ContinueWatchingFeedCandidate(
    Guid MediaId,
    DateTime SortAt,
    Guid GroupId,
    bool IsItemBookmark = false);

/// <summary>
/// Continue Watching must emit one card per series/movie. Item bookmarks on episodes
/// used the episode id as GroupId, so the same episode also appeared via the series
/// next-up bookmark and Blazor carousel @key collided.
/// An in-progress episode bookmark always wins over series next-up for the same series,
/// even when the series bookmark was refreshed more recently (scan / backfill).
/// </summary>
internal static class ContinueWatchingFeedDeduper
{
    public static List<ContinueWatchingFeedCandidate> Deduplicate(
        IEnumerable<ContinueWatchingFeedCandidate> candidates)
    {
        return [.. candidates
            .GroupBy(c => c.GroupId)
            .Select(g => g
                .OrderByDescending(c => c.IsItemBookmark)
                .ThenByDescending(c => c.SortAt)
                .ThenByDescending(c => c.MediaId)
                .First())
            .GroupBy(c => c.MediaId)
            .Select(g => g
                .OrderByDescending(c => c.IsItemBookmark)
                .ThenByDescending(c => c.SortAt)
                .ThenByDescending(c => c.GroupId)
                .First())];
    }
}
