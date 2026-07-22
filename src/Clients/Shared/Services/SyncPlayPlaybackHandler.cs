using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Coordinates local player state with the SyncPlay group.
///
/// Design:
/// - Sync happens ONLY at state transitions (Play, Pause, Seek, PlayAt).
/// - No continuous position polling or server-side drift detection.
/// - After a PlayAt, clients are trusted to stay in sync (HLS keyframes may differ by a few seconds - that is acceptable).
/// - A cooldown window after every SyncPlay-initiated action suppresses local state change events to prevent feedback loops.
/// </summary>
public sealed class SyncPlayPlaybackHandler : IDisposable
{
    private readonly ISyncPlayService _syncPlay;
    private readonly IAudioPlayerService _audio;
    private readonly IPlayerService _video;
    private readonly ISyncPlayMediaLoader _mediaLoader;
    private readonly ILogger<SyncPlayPlaybackHandler> _logger;

    // Core state flags (reduced from 6 to 3)
    private bool _suppressingEvents;      // True while cooldown is active after a SyncPlay action
    private bool _waitingForReady;        // True while loading media / seeking before reporting ready
    private bool _isSyncPlaySeeking;      // True during programmatic seeks to suppress SeekRequested events
    private bool _loadingQueue;           // True while loading audio queue
    private bool _pendingGroupSync;       // True when local media change needs server ack

    private Guid? _lastLoadedMediaId;
    private bool _wasInGroup;
    private long _suppressUntilTick;      // Environment.TickCount64 after which events are no longer suppressed
    private Timer? _readyTimeoutTimer;

    private const int SuppressCooldownMs = 1500; // Ignore state events for 1.5s after any SyncPlay action

    public SyncPlayPlaybackHandler(ISyncPlayService syncPlay, IAudioPlayerService audio, IPlayerService video, ISyncPlayMediaLoader mediaLoader, ILogger<SyncPlayPlaybackHandler> logger)
    {
        _syncPlay = syncPlay;
        _audio = audio;
        _video = video;
        _mediaLoader = mediaLoader;
        _logger = logger;

        _syncPlay.CommandReceived += OnCommandReceived;
        _syncPlay.PlayAtReceived += OnPlayAtReceived;
        _syncPlay.SeekCorrectionReceived += OnSeekCorrectionReceived;
        _syncPlay.GroupUpdated += OnGroupUpdated;
        _syncPlay.RejoinRequested += OnRejoinRequested;

        _audio.PlaybackStateChanged += OnAudioStateChanged;
        _audio.CurrentTrackChanged += OnAudioTrackChanged;
        _audio.SeekRequested += OnAudioSeekRequested;
        _video.PlaybackStateChanged += OnVideoStateChanged;
        _video.SourceChanged += OnVideoSourceChanged;
        _video.SeekRequested += OnVideoSeekRequested;
    }

    private bool IsAudioActive => _audio.IsVisible;
    private bool IsVideoActive => _video.IsVisible;
    private bool IsActive => _syncPlay.IsInGroup && (IsAudioActive || IsVideoActive);

    private bool IsSuppressed => _suppressingEvents && Environment.TickCount64 < _suppressUntilTick;

    private void BeginSuppress()
    {
        _suppressingEvents = true;
        _suppressUntilTick = Environment.TickCount64 + SuppressCooldownMs;
    }

    // --- Group state handling ---

    private void OnGroupUpdated()
    {
        if (_syncPlay.IsInGroup)
        {
            var justJoined = !_wasInGroup;
            _wasInGroup = true;

            if (justJoined)
            {
                _ = SyncQueueOnJoinAsync();
            }
            else
            {
                CheckMediaChange();
                CheckQueueSync();
            }
        }
        else
        {
            _wasInGroup = false;
            _lastLoadedMediaId = null;
        }
    }

    private async Task SyncQueueOnJoinAsync()
    {
        var group = _syncPlay.CurrentGroup;
        if (group is null) return;

        // If we're the creator (audio is active with a queue), upload the local queue to the server
        if (IsAudioActive && _audio.Queue.Count > 1 && group.Queue.Count <= 1)
        {
            _lastLoadedMediaId = _audio.CurrentTrack?.MediaId;

            var items = _audio.Queue
                .Where(t => t.MediaId != group.CurrentMedia?.MediaReferenceId)
                .Select(t => new SyncPlayQueueItemDto
                {
                    QueueItemId = Guid.NewGuid(),
                    MediaReferenceId = t.MediaId,
                    Title = t.Title,
                    Duration = t.Duration ?? 0,
                    CoverUrl = t.CoverUrl
                })
                .ToList();

            if (items.Count > 0)
                await _syncPlay.BulkAddToQueueAsync(items);
        }
        // If we're a joiner and the server has a queue, load it
        else if (group.Queue.Count > 1 && group.CurrentMedia is not null)
        {
            var currentIndex = group.Queue.ToList().FindIndex(q => q.QueueItemId == group.CurrentMedia.QueueItemId);
            if (currentIndex < 0) currentIndex = 0;

            _lastLoadedMediaId = group.CurrentMedia.MediaReferenceId;
            _waitingForReady = true;
            BeginSuppress();

            _loadingQueue = true;
            try
            {
                await _mediaLoader.LoadQueueAsync(group.Queue, currentIndex);
            }
            finally
            {
                _loadingQueue = false;
            }

            // After queue loads, sync position
            SyncPositionToGroup(group);
        }
        else
        {
            CheckMediaChange();
        }
    }

    private void CheckMediaChange()
    {
        var currentMedia = _syncPlay.CurrentGroup?.CurrentMedia;
        if (currentMedia is null) return;

        if (_lastLoadedMediaId == currentMedia.MediaReferenceId) return;

        // Check if local player already has this media loaded
        var localMediaId = GetLocalMediaId();
        if (localMediaId == currentMedia.MediaReferenceId)
        {
            _lastLoadedMediaId = currentMedia.MediaReferenceId;
            var group = _syncPlay.CurrentGroup;
            if (group is not null)
                SyncPositionToGroup(group);
            return;
        }

        _lastLoadedMediaId = currentMedia.MediaReferenceId;
        _waitingForReady = true;
        BeginSuppress();

        // If the audio player has a queue, try to skip to the matching track
        if (IsAudioActive && _audio.Queue.Count > 1)
        {
            var localIndex = _audio.Queue.ToList().FindIndex(q => q.MediaId == currentMedia.MediaReferenceId);
            if (localIndex >= 0)
            {
                _ = SkipToIndexAndReportReadyAsync(localIndex);
                return;
            }
        }

        _ = LoadMediaAsync(currentMedia);
    }

    private async Task SkipToIndexAndReportReadyAsync(int index)
    {
        await _audio.SkipToIndexAsync(index);
        // Give the player a moment to transition
        await Task.Delay(500);
        ReportReadyNow();
    }

    private void CheckQueueSync()
    {
        var group = _syncPlay.CurrentGroup;
        if (group is null || _loadingQueue) return;
        if (group.Queue.Count <= 1 || group.CurrentMedia is null) return;

        // Only sync the audio queue when the group is playing music.
        // If the current media is a video, skip queue sync to avoid reloading audio.
        if (IsVideoActive) return;

        var localQueueCount = IsAudioActive ? _audio.Queue.Count : 0;
        if (localQueueCount >= group.Queue.Count) return;

        var currentIndex = group.Queue.ToList().FindIndex(q => q.QueueItemId == group.CurrentMedia.QueueItemId);
        if (currentIndex < 0) currentIndex = 0;

        _lastLoadedMediaId = group.CurrentMedia.MediaReferenceId;
        _loadingQueue = true;
        _ = LoadQueueSilentAsync(group.Queue, currentIndex);
    }

    private async Task LoadQueueSilentAsync(IReadOnlyList<SyncPlayQueueItemDto> queue, int currentIndex)
    {
        try
        {
            await _mediaLoader.LoadQueueAsync(queue, currentIndex);
        }
        finally
        {
            _loadingQueue = false;
        }
    }

    private Guid? GetLocalMediaId()
    {
        if (IsVideoActive && _video.Source?.MediaId is { } videoMediaId)
            return videoMediaId;
        if (IsAudioActive && _audio.CurrentTrack?.MediaId is { } audioMediaId)
            return audioMediaId;
        return null;
    }

    private async Task LoadMediaAsync(SyncPlayQueueItemDto media)
    {
        try
        {
            await _mediaLoader.LoadAndPlayMediaAsync(media.MediaReferenceId, media.Title, media.CoverUrl);

            // Kick the player after a short delay if it hasn't reported ready
            _ = KickPlayerIfStalled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SyncPlay] Failed to load media {MediaId}", media.MediaReferenceId);
            _waitingForReady = false;
            await _syncPlay.ReportReadyAsync();
        }
    }

    private async Task KickPlayerIfStalled()
    {
        await Task.Delay(3000);

        if (!_waitingForReady) return;
        if (!IsActive) return;

        _logger.LogDebug("[SyncPlay] KickPlayerIfStalled: player still waiting, forcing ready report");

        // Force ready - the player likely loaded but the state event was swallowed
        ReportReadyNow();
    }

    private void SyncPositionToGroup(SyncPlayGroupDto group)
    {
        BeginSuppress();
        SeekLocal(group.Position);
        if (group.State == SyncPlayGroupState.Playing)
            PlayLocal();
        else
            PauseLocal();

        // Report ready since we synced
        _ = _syncPlay.ReportReadyAsync();
    }

    // --- Server commands ---

    private void OnCommandReceived(SyncPlayCommandDto command)
    {
        _logger.LogDebug("[SyncPlay] OnCommandReceived: {Type}, value={Value}, isActive={Active}", command.CommandType, command.Value, IsActive);

        if (!IsActive)
        {
            // Report ready immediately - we have nothing to buffer
            _ = _syncPlay.ReportReadyAsync();
            return;
        }

        BeginSuppress();

        switch (command.CommandType)
        {
            case SyncPlayCommandType.Pause:
                PauseLocal();
                break;

            case SyncPlayCommandType.Play:
                PlayLocal();
                break;

            case SyncPlayCommandType.SeekTo:
                if (command.Value is { } seekPos)
                {
                    PauseLocal();
                    SeekLocal(seekPos);
                }
                _waitingForReady = true;
                ReportReadyAfterDelay();
                break;
        }
    }

    private void OnPlayAtReceived(long serverTimestampMs, double position)
    {
        _logger.LogDebug("[SyncPlay] OnPlayAtReceived: position={Pos}, serverTs={Ts}", position, serverTimestampMs);

        if (!IsActive) return;

        // Clear any pending state
        _readyTimeoutTimer?.Dispose();
        _readyTimeoutTimer = null;
        _waitingForReady = false;

        BeginSuppress();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var delayMs = serverTimestampMs - now;

        if (delayMs <= 0)
        {
            // We're late - seek to compensated position and play immediately
            var elapsed = (now - serverTimestampMs) / 1000.0;
            SeekLocal(position + elapsed);
            PlayLocal();
        }
        else
        {
            // Seek now, play after delay
            SeekLocal(position);
            _ = DelayedPlay(delayMs);
        }
    }

    private async Task DelayedPlay(long delayMs)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
        BeginSuppress();
        PlayLocal();
    }

    private void OnSeekCorrectionReceived(double position)
    {
        // Legacy - kept for backward compatibility but should not fire anymore
        if (!IsActive) return;

        _logger.LogDebug("[SyncPlay] OnSeekCorrectionReceived: target={Target}", position);
        BeginSuppress();
        SeekLocal(position);
    }

    // --- Local player state change handling ---

    private void OnAudioStateChanged(PlaybackState state)
    {
        if (!_syncPlay.IsInGroup || !IsAudioActive) return;
        if (state == PlaybackState.Buffering) return;

        HandleLocalStateChange(state, _audio.CurrentTime);
    }

    private void OnVideoStateChanged(PlaybackState state)
    {
        if (!_syncPlay.IsInGroup || !IsVideoActive) return;
        if (state == PlaybackState.Buffering) return;

        HandleLocalStateChange(state, _video.CurrentTime);
    }

    private void HandleLocalStateChange(PlaybackState state, double currentTime)
    {
        // If we're waiting for media to load and player transitions to a usable state, report ready
        if (_waitingForReady && state is PlaybackState.Playing or PlaybackState.Paused)
        {
            _logger.LogDebug("[SyncPlay] Player ready (state={State}), reporting ready", state);
            ReportReadyNow();
            return;
        }

        // If we're in the suppression window, swallow the event
        if (IsSuppressed)
        {
            _logger.LogDebug("[SyncPlay] State change suppressed: {State}", state);
            return;
        }

        // If pending group sync (local track change that server needs to know about)
        if (_pendingGroupSync && state == PlaybackState.Playing)
        {
            _pendingGroupSync = false;
            _waitingForReady = true;
            BeginSuppress();
            SeekLocal(0);
            _ = _syncPlay.ReportReadyAsync();
            return;
        }

        // This is a genuine user action - broadcast it
        _logger.LogDebug("[SyncPlay] Broadcasting user action: {State} at {Pos}", state, currentTime);
        if (state == PlaybackState.Playing)
            _ = _syncPlay.IssueCommandAsync(SyncPlayCommandType.Play);
        else if (state == PlaybackState.Paused)
            _ = _syncPlay.IssueCommandAsync(SyncPlayCommandType.Pause, currentTime);
    }

    // --- Ready reporting ---

    private void ReportReadyAfterDelay()
    {
        _readyTimeoutTimer?.Dispose();
        _readyTimeoutTimer = new Timer(OnReadyTimeout, null, TimeSpan.FromMilliseconds(1500), Timeout.InfiniteTimeSpan);
    }

    private void ReportReadyNow()
    {
        _waitingForReady = false;
        _readyTimeoutTimer?.Dispose();
        _readyTimeoutTimer = null;
        _ = _syncPlay.ReportReadyAsync();
    }

    private void OnReadyTimeout(object? _)
    {
        if (!_waitingForReady) return;
        _waitingForReady = false;
        _ = _syncPlay.ReportReadyAsync();
    }

    // --- Local player control ---

    private void PlayLocal()
    {
        if (IsAudioActive)
            _audio.Play();
        else if (IsVideoActive)
            _video.Play();
    }

    private void PauseLocal()
    {
        if (IsAudioActive)
            _audio.Pause();
        else if (IsVideoActive)
            _video.Pause();
    }

    private void SeekLocal(double position)
    {
        _isSyncPlaySeeking = true;
        if (IsAudioActive)
            _audio.Seek(position);
        else if (IsVideoActive)
            _video.Seek(position);
        _isSyncPlaySeeking = false;
    }

    // --- User-initiated seeks ---

    private void OnVideoSourceChanged(PlayerSource source)
    {
        if (!_syncPlay.IsInGroup) return;
        if (source.MediaId is null) return;
        if (source.MediaId == _lastLoadedMediaId) return;

        _lastLoadedMediaId = source.MediaId;
        _pendingGroupSync = true;
        _ = _syncPlay.SetCurrentMediaAsync(source.MediaId.Value, source.Title ?? "", _video.Duration, source.CoverUrl);
    }

    private void OnAudioTrackChanged(AudioQueueItem? track)
    {
        if (!_syncPlay.IsInGroup) return;
        if (_loadingQueue) return;
        if (track is null) return;
        if (track.MediaId == _lastLoadedMediaId) return;

        _lastLoadedMediaId = track.MediaId;
        _pendingGroupSync = true;
        _ = _syncPlay.SetCurrentMediaAsync(track.MediaId, track.Title, track.Duration ?? 0, track.CoverUrl);
    }

    private Task OnVideoSeekRequested(double time)
    {
        if (_isSyncPlaySeeking || !_syncPlay.IsInGroup || !IsVideoActive) return Task.CompletedTask;
        _logger.LogDebug("[SyncPlay] User seeked video to {Time}", time);
        BeginSuppress();
        _ = _syncPlay.IssueCommandAsync(SyncPlayCommandType.SeekTo, time);
        PauseLocal();
        _waitingForReady = true;
        ReportReadyAfterDelay();
        return Task.CompletedTask;
    }

    private Task OnAudioSeekRequested(double time)
    {
        if (_isSyncPlaySeeking || !_syncPlay.IsInGroup || !IsAudioActive) return Task.CompletedTask;
        BeginSuppress();
        _ = _syncPlay.IssueCommandAsync(SyncPlayCommandType.SeekTo, time);
        PauseLocal();
        _waitingForReady = true;
        ReportReadyAfterDelay();
        return Task.CompletedTask;
    }

    private void OnRejoinRequested()
    {
        var currentMedia = _syncPlay.CurrentGroup?.CurrentMedia;
        if (currentMedia is null) return;

        _lastLoadedMediaId = currentMedia.MediaReferenceId;
        _waitingForReady = true;
        BeginSuppress();
        _ = LoadMediaAsync(currentMedia);
    }

    public void Dispose()
    {
        _syncPlay.CommandReceived -= OnCommandReceived;
        _syncPlay.PlayAtReceived -= OnPlayAtReceived;
        _syncPlay.SeekCorrectionReceived -= OnSeekCorrectionReceived;
        _syncPlay.GroupUpdated -= OnGroupUpdated;
        _syncPlay.RejoinRequested -= OnRejoinRequested;
        _audio.PlaybackStateChanged -= OnAudioStateChanged;
        _audio.CurrentTrackChanged -= OnAudioTrackChanged;
        _audio.SeekRequested -= OnAudioSeekRequested;
        _video.PlaybackStateChanged -= OnVideoStateChanged;
        _video.SourceChanged -= OnVideoSourceChanged;
        _video.SeekRequested -= OnVideoSeekRequested;
        _readyTimeoutTimer?.Dispose();
    }
}
