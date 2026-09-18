using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Android Auto radio fast-starts on one Media3 item. Upcoming K7 queue rows
/// must be appended after that, and again after a player swap (crossfade/gapless)
/// which leaves a single MediaItem on the session player. Already-played rows
/// must be prepended too. Otherwise Auto previous only restarts the current
/// title (native index 0). Always derive loaded ids from the live player
/// timeline. Media3 setMediaItems of the fast-start list can wipe extras while
/// a stale HashSet still thinks they are loaded.
/// </summary>
public static class AndroidAutoRadioPlaylist
{
    public readonly record struct Gap(
        IReadOnlyList<AudioQueueItem> ToPrepend,
        IReadOnlyList<AudioQueueItem> ToAppend)
    {
        public int Count => ToPrepend.Count + ToAppend.Count;
    }

    public static HashSet<Guid> ParsePlayerMediaIds(IEnumerable<string?> mediaIds)
    {
        ArgumentNullException.ThrowIfNull(mediaIds);

        var ids = new HashSet<Guid>();
        foreach (var mediaId in mediaIds)
        {
            if (mediaId is not null && Guid.TryParse(mediaId, out var id))
                ids.Add(id);
        }

        return ids;
    }

    public static bool ShouldRoutePreviousThroughQueue(int nativeIndex, bool hasPreviousInQueue) =>
        nativeIndex <= 0 && hasPreviousInQueue;

    public static Gap MissingFromPlayer(
        IReadOnlyList<AudioQueueItem> queue,
        int currentIndex,
        IReadOnlySet<Guid> mediaIdsOnPlayer)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(mediaIdsOnPlayer);

        if (queue.Count == 0)
            return new([], []);

        var current = Math.Clamp(currentIndex, 0, queue.Count);
        var prepend = new List<AudioQueueItem>();
        var append = new List<AudioQueueItem>();
        for (var i = 0; i < queue.Count; i++)
        {
            var track = queue[i];
            if (mediaIdsOnPlayer.Contains(track.MediaId))
                continue;

            if (i < current)
                prepend.Add(track);
            else
                append.Add(track);
        }

        return new(prepend, append);
    }

    public static IReadOnlyList<AudioQueueItem> UpcomingNotOnPlayer(
        IReadOnlyList<AudioQueueItem> queue,
        int currentIndex,
        IReadOnlySet<Guid> mediaIdsOnPlayer) =>
        MissingFromPlayer(queue, currentIndex, mediaIdsOnPlayer).ToAppend;
}
