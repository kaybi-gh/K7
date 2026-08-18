namespace K7.Clients.Shared.Interfaces;

/// <summary>
/// Flushes locally queued offline playback progress and ratings to the server.
/// </summary>
public interface IPlaybackSyncService
{
    Task SyncPendingEventsAsync(CancellationToken cancellationToken = default);
}
