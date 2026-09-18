using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Resolves queue metadata for a native player source. Gapless prebuffer prepares
/// the next URL while <c>CurrentTrack</c> is still the outgoing title.
/// </summary>
public static class AudioPlayerSourceTrack
{
    public static AudioQueueItem? Resolve(
        IReadOnlyList<AudioQueueItem>? queue,
        PlayerSource source,
        AudioQueueItem? currentTrack,
        AudioQueueItem? pendingTrack)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (queue is not null && queue.Count > 0)
        {
            if (source.MediaId is { } mediaId)
            {
                for (var i = 0; i < queue.Count; i++)
                {
                    if (queue[i].MediaId == mediaId)
                        return queue[i];
                }
            }

            if (source.IndexedFileId is { } fileId)
            {
                for (var i = 0; i < queue.Count; i++)
                {
                    if (queue[i].IndexedFileId == fileId)
                        return queue[i];
                }
            }
        }

        return currentTrack ?? pendingTrack;
    }
}
