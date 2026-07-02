using K7.Clients.Shared.Interfaces;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Clients.MAUI.Services;

public class PlaybackSyncService
{
    private readonly IPlaybackJournal _journal;
    private readonly IStreamingService _streamingService;
    private readonly IRatingService _ratingService;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<PlaybackSyncService> _logger;

    public PlaybackSyncService(
        IPlaybackJournal journal,
        IStreamingService streamingService,
        IRatingService ratingService,
        IConnectivityService connectivity,
        ILogger<PlaybackSyncService> logger)
    {
        _journal = journal;
        _streamingService = streamingService;
        _ratingService = ratingService;
        _connectivity = connectivity;
        _logger = logger;

        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(bool isOnline)
    {
        if (isOnline)
        {
            await SyncPendingEventsAsync();
        }
    }

    public async Task SyncPendingEventsAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline) return;

        try
        {
            var pendingEvents = await _journal.GetPendingEventsAsync(cancellationToken);
            if (pendingEvents.Count == 0) return;

            _logger.LogInformation("Syncing {Count} pending playback events", pendingEvents.Count);

            var syncedIds = new List<Guid>();

            foreach (var evt in pendingEvents)
            {
                try
                {
                    if (evt.EventType == PlaybackEventType.Rated && evt.RatingValue.HasValue)
                    {
                        await _ratingService.RateMediaAsync(evt.MediaId, evt.RatingValue.Value, cancellationToken);
                    }
                    else
                    {
                        var state = evt.EventType switch
                        {
                            PlaybackEventType.Completed => 5, // Ended
                            PlaybackEventType.Skipped => 4, // Paused
                            _ => 3 // Playing
                        };

                        await _streamingService.ReportPlaybackProgressAsync(
                            evt.MediaId,
                            Guid.Empty,
                            evt.IndexedFileId,
                            evt.Position,
                            evt.Duration,
                            state,
                            viewingGroupId: evt.ViewingGroupId,
                            cancellationToken: cancellationToken);
                    }

                    syncedIds.Add(evt.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync event {EventId}, will retry later", evt.Id);
                    break; // Stop on first failure to maintain order
                }
            }

            if (syncedIds.Count > 0)
            {
                await _journal.MarkSyncedAsync(syncedIds, cancellationToken);
                _logger.LogInformation("Successfully synced {Count} playback events", syncedIds.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playback sync failed");
        }
    }
}
