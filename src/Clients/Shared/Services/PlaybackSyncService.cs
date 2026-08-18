using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Replays queued offline progress and ratings for the currently signed-in online user.
/// Does not run during splash or on select-profile (anonymous / offline sessions).
/// </summary>
public class PlaybackSyncService : IPlaybackSyncService
{
    private readonly IPlaybackJournal _journal;
    private readonly IStreamingService _streamingService;
    private readonly IRatingService _ratingService;
    private readonly IConnectivityService _connectivity;
    private readonly AuthenticationStateProvider _auth;
    private readonly ILogger<PlaybackSyncService> _logger;
    private int _syncInFlight;

    public PlaybackSyncService(
        IPlaybackJournal journal,
        IStreamingService streamingService,
        IRatingService ratingService,
        IConnectivityService connectivity,
        AuthenticationStateProvider auth,
        ILogger<PlaybackSyncService> logger)
    {
        _journal = journal;
        _streamingService = streamingService;
        _ratingService = ratingService;
        _connectivity = connectivity;
        _auth = auth;
        _logger = logger;

        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(bool isOnline) => OnConnectivityChangedAsync(isOnline).FireAndForget(_logger);

    private async Task OnConnectivityChangedAsync(bool isOnline)
    {
        if (isOnline)
            await SyncPendingEventsAsync();
    }

    public async Task SyncPendingEventsAsync(CancellationToken cancellationToken = default)
    {
        if (!AppReadySignal.IsSignaled)
            return;

        if (!_connectivity.IsOnline)
            return;

        if (Interlocked.CompareExchange(ref _syncInFlight, 1, 0) != 0)
            return;

        try
        {
            var identityUserId = await AuthIdentity.GetOnlineUserIdAsync(_auth, cancellationToken);
            if (string.IsNullOrEmpty(identityUserId))
                return;

            var pendingEvents = await _journal.GetPendingEventsAsync(identityUserId, cancellationToken);
            if (pendingEvents.Count == 0)
                return;

            _logger.LogInformation(
                "Syncing {Count} pending playback events for user {IdentityUserId}",
                pendingEvents.Count,
                identityUserId);

            var syncedIds = new List<Guid>();

            foreach (var evt in pendingEvents)
            {
                if (!string.Equals(evt.IdentityUserId, identityUserId, StringComparison.Ordinal))
                    continue;

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
                            Guid.NewGuid(),
                            evt.IndexedFileId,
                            evt.Position,
                            evt.Duration,
                            state,
                            sharedProfileId: evt.SharedProfileId,
                            cancellationToken: cancellationToken);
                    }

                    syncedIds.Add(evt.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync event {EventId}, will retry later", evt.Id);
                    break;
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
        finally
        {
            Interlocked.Exchange(ref _syncInFlight, 0);
        }
    }
}
