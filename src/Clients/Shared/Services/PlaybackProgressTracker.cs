using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Tracks playback progress and periodically reports it to the server.
/// Hooks into IPlayerService events to detect time updates, pause, and stop.
/// Skips reporting when progress reporting is disabled for the current user
/// (unauthenticated, or lacking CanReportPlaybackProgress).
/// </summary>
public class PlaybackProgressTracker : IDisposable
{
    private readonly IPlayerService _playerService;
    private readonly IStreamingService _serverService;
    private readonly IDeviceStorageService _deviceStorage;
    private readonly IConnectivityService _connectivity;
    private readonly IPlaybackJournal _journal;
    private readonly ILocalUserService _localUsers;
    private readonly ISharedProfileSessionService? _viewingGroupSession;
    private readonly ISyncPlayService? _syncPlayService;
    private readonly MediaCacheStore _cacheStore;
    private Timer? _reportTimer;
    private Timer? _bufferingTimer;
    private Guid? _currentMediaId;
    private Guid? _currentSerieId;
    private Guid _sessionId;
    private Guid _referenceId;
    private Guid? _currentIndexedFileId;
    private double _lastReportedPosition;
    private double _lastKnownTime;
    private double _resumeFloor;
    private bool _isAuthenticated;
    private bool _disposed;
    private bool _hadMeaningfulPlayback;
    private bool _bufferingWatchActive;
    private bool _sustainedBufferingReported;
    private PlaybackState _lastState = PlaybackState.Unknown;

    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(10);
    /// <summary>Only report Buffering after it has lasted this long (Tautulli-style sustained buffer).</summary>
    internal static TimeSpan SustainedBufferingThreshold { get; set; } = TimeSpan.FromSeconds(15);
    private const double MinPositionDeltaToReport = 2.0;
    private const double SeekDetectionThreshold = 3.0;
    private const double SpuriousZeroGuardSeconds = 5.0;
    private const double SignificantProgressSeconds = 30.0;

    public Guid? CurrentMediaId => _currentMediaId;
    public Guid? CurrentSerieId => _currentSerieId;

    public PlaybackProgressTracker(
        IPlayerService playerService,
        IStreamingService serverService,
        IDeviceStorageService deviceStorage,
        IConnectivityService connectivity,
        IPlaybackJournal journal,
        ILocalUserService localUsers,
        MediaCacheStore cacheStore,
        ISharedProfileSessionService? viewingGroupSession = null,
        ISyncPlayService? syncPlayService = null)
    {
        _playerService = playerService;
        _serverService = serverService;
        _deviceStorage = deviceStorage;
        _connectivity = connectivity;
        _journal = journal;
        _localUsers = localUsers;
        _cacheStore = cacheStore;
        _viewingGroupSession = viewingGroupSession;
        _syncPlayService = syncPlayService;

        _playerService.PlaybackStateChanged += OnPlaybackStateChanged;
        _playerService.CurrentTimeChanged += OnCurrentTimeChanged;
        _playerService.SourceChanged += OnSourceChanged;
        _playerService.AudioTrackChanged += OnSelectedTrackChanged;
        _playerService.SubtitleTrackChanged += OnSelectedSubtitleChanged;
    }

    /// <summary>
    /// Begins tracking a specific media. Call this when a new media starts playing.
    /// </summary>
    /// <param name="mediaId">The media being played.</param>
    /// <param name="isAuthenticated">Whether progress reporting is enabled for the current user. When false, progress is not reported.</param>
    /// <param name="serieId">Optional serie ID when playing a serie episode.</param>
    public void StartTracking(Guid mediaId, bool isAuthenticated = true, Guid? serieId = null, Guid? indexedFileId = null)
    {
        StopTimer();
        _currentMediaId = mediaId;
        _currentSerieId = serieId;
        _currentIndexedFileId = indexedFileId;
        _sessionId = _playerService.Source?.StreamSessionId ?? Guid.NewGuid();
        _referenceId = Guid.NewGuid();
        _lastReportedPosition = 0;
        _isAuthenticated = isAuthenticated;
        _hadMeaningfulPlayback = false;
        _lastState = PlaybackState.Unknown;
        CancelBufferingWatch();
        ApplyResumeFloor(_playerService.Source?.PendingSeekTime);
        StartTimer();
    }

    /// <summary>
    /// Stops tracking and sends a final progress report.
    /// </summary>
    public void StopTracking() => StopTrackingAsync().FireAndForget();

    private async Task StopTrackingAsync()
    {
        StopTimer();
        CancelBufferingWatch();
        var mediaId = _currentMediaId;
        _currentMediaId = null;
        _currentSerieId = null;
        if (mediaId is not null)
        {
            await ReportProgressAsync(mediaId.Value, force: true, state: PlaybackState.Idle);
            _cacheStore.InvalidateHomeFeed();
        }
    }

    private void OnCurrentTimeChanged(double time)
    {
        if (ShouldIgnoreTransientPosition(time))
            return;

        // Detect significant seek (forward or backward) and immediately report
        if (_currentMediaId is not null && Math.Abs(time - _lastKnownTime) > SeekDetectionThreshold)
        {
            _ = ReportProgressAsync(force: false, state: ResolveReportState(_lastState));
        }

        _lastKnownTime = time;
        ClearResumeFloorIfReached(time);
    }

    private void OnSourceChanged(PlayerSource source)
    {
        // Use the server's StreamSessionId so progress reports match the stream tracker
        if (source.StreamSessionId is { } serverSessionId)
        {
            _sessionId = serverSessionId;
        }

        ApplyResumeFloor(source.PendingSeekTime);
    }

    private void OnPlaybackStateChanged(PlaybackState state)
    {
        if (state == PlaybackState.Buffering)
        {
            StartBufferingWatch();
            return;
        }

        CancelBufferingWatch();
        _lastState = state;
        switch (state)
        {
            case PlaybackState.Paused:
                _hadMeaningfulPlayback = true;
                StopTimer();
                _ = ReportProgressAsync(force: true, state: state);
                break;
            case PlaybackState.Playing:
                _hadMeaningfulPlayback = true;
                _ = ReportProgressAsync(force: true, state: state);
                StartTimer();
                break;
            case PlaybackState.Idle:
                StopTimer();
                // Ignore startup Idle from source swaps before the first Playing/Paused.
                if (_hadMeaningfulPlayback)
                    _ = ReportProgressAsync(force: true, state: state);
                break;
            case PlaybackState.Ended:
                StopTimer();
                _hadMeaningfulPlayback = true;
                _ = ReportProgressAsync(force: true, state: state);
                break;
        }
    }

    private void StartBufferingWatch()
    {
        // Brief waiting/stalled/seek spikes are ignored. Only sustained buffering is reported.
        if (!_hadMeaningfulPlayback || _bufferingWatchActive || _sustainedBufferingReported)
            return;

        _bufferingWatchActive = true;
        _bufferingTimer?.Dispose();
        _bufferingTimer = new Timer(
            _ => OnSustainedBufferingElapsed(),
            null,
            SustainedBufferingThreshold,
            Timeout.InfiniteTimeSpan);
    }

    private void OnSustainedBufferingElapsed()
    {
        if (_disposed || !_bufferingWatchActive)
            return;

        _bufferingWatchActive = false;
        _sustainedBufferingReported = true;
        _lastState = PlaybackState.Buffering;
        _ = ReportProgressAsync(force: true, state: PlaybackState.Buffering);
    }

    private void CancelBufferingWatch()
    {
        _bufferingWatchActive = false;
        _sustainedBufferingReported = false;
        _bufferingTimer?.Dispose();
        _bufferingTimer = null;
    }

    private void StartTimer()
    {
        _reportTimer ??= new Timer(
            _ => _ = ReportProgressAsync(force: false, state: ResolveReportState(_lastState)),
            null,
            ReportInterval,
            ReportInterval);
    }

    private void StopTimer()
    {
        _reportTimer?.Dispose();
        _reportTimer = null;
    }

    private void OnSelectedTrackChanged(AudioFileTrackDto? _) => ReportSelectedTracks();

    private void OnSelectedSubtitleChanged(SubtitleFileTrackDto? _) => ReportSelectedTracks();

    private void ReportSelectedTracks()
    {
        if (_lastState is PlaybackState.Playing or PlaybackState.Paused)
            _ = ReportProgressAsync(force: true, state: _lastState);
    }

    private async Task ReportProgressAsync(bool force = false, PlaybackState? state = null) =>
        await ReportProgressAsync(_currentMediaId, force, state);

    private async Task ReportProgressAsync(Guid? mediaId, bool force = false, PlaybackState? state = null)
    {
        if (mediaId is null) return;
        if (!_isAuthenticated) return;

        var reportState = ResolveReportState(state ?? _lastState);
        if (reportState is PlaybackState.Unknown)
            return;
        // Buffering is only sent after the sustained threshold (force report).
        if (reportState is PlaybackState.Buffering && !force)
            return;

        // Prefer last known time: CurrentTime can briefly drop when the player is disposed on Idle.
        var position = _playerService.CurrentTime > 0 ? _playerService.CurrentTime : _lastKnownTime;
        var duration = _playerService.Duration;
        var isTerminal = reportState is PlaybackState.Idle or PlaybackState.Ended;

        try
        {
            if (duration <= 0) return;
            if (ShouldIgnoreTransientPosition(position)) return;
            if (!force && !isTerminal && Math.Abs(position - _lastReportedPosition) < MinPositionDeltaToReport) return;

            // Never overwrite a solid resume point with a near-zero tick after reopen/rebind.
            if (!isTerminal
                && position < SpuriousZeroGuardSeconds
                && _lastReportedPosition > SignificantProgressSeconds)
            {
                return;
            }

            _lastReportedPosition = position;
            ClearResumeFloorIfReached(position);

            var identityUserId = _localUsers.GetLastActive()?.IdentityUserId;

            if (!_connectivity.IsOnline && _currentIndexedFileId.HasValue)
            {
                if (!string.IsNullOrEmpty(identityUserId))
                {
                    await _journal.RecordProgressAsync(
                        mediaId.Value,
                        _currentIndexedFileId.Value,
                        position,
                        duration,
                        identityUserId,
                        _viewingGroupSession?.ActiveGroupId);
                }

                return;
            }

            try
            {
                var deviceIdStr = _deviceStorage.Get(PreferenceKeys.DEVICE_ID);
                Guid? deviceId = Guid.TryParse(deviceIdStr, out var parsed) ? parsed : null;
                await _serverService.ReportPlaybackProgressAsync(
                    mediaId.Value,
                    _sessionId,
                    _referenceId,
                    position,
                    duration,
                    (int)reportState,
                    deviceId,
                    sharedProfileId: _viewingGroupSession?.ActiveGroupId,
                    syncPlayGroupId: _syncPlayService?.IsInGroup == true ? _syncPlayService.CurrentGroup?.GroupId : null,
                    audioTrackIndex: _playerService.SelectedAudioTrack?.Index,
                    subtitleTrackIndex: _playerService.SelectedSubtitleTrack?.Index);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to report playback progress: {ex.Message}");
                if (_currentIndexedFileId.HasValue && !string.IsNullOrEmpty(identityUserId))
                {
                    await _journal.RecordProgressAsync(
                        mediaId.Value,
                        _currentIndexedFileId.Value,
                        position,
                        duration,
                        identityUserId,
                        _viewingGroupSession?.ActiveGroupId);
                }
            }
        }
        finally
        {
            // Always refresh Keep Watching when playback stops, even if the last tick
            // already reported nearly the same position (delta gate would skip the POST).
            if (isTerminal)
                _cacheStore.InvalidateHomeFeed();
        }
    }

    private PlaybackState ResolveReportState(PlaybackState state)
    {
        // While watching for sustained buffering, keep reporting Playing for heartbeats/seeks.
        if (state == PlaybackState.Buffering && !_sustainedBufferingReported)
            return _hadMeaningfulPlayback ? PlaybackState.Playing : PlaybackState.Unknown;

        return state;
    }

    private void ApplyResumeFloor(double? pendingSeekTime)
    {
        if (pendingSeekTime is > 0)
        {
            _resumeFloor = pendingSeekTime.Value;
            if (_lastKnownTime < _resumeFloor)
                _lastKnownTime = _resumeFloor;
            return;
        }

        _resumeFloor = 0;
    }

    private void ClearResumeFloorIfReached(double position)
    {
        if (_resumeFloor > 0 && position >= _resumeFloor - SeekDetectionThreshold)
            _resumeFloor = 0;
    }

    private bool ShouldIgnoreTransientPosition(double position)
    {
        if (_resumeFloor > 0 && position < _resumeFloor - SeekDetectionThreshold)
            return true;

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _playerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _playerService.CurrentTimeChanged -= OnCurrentTimeChanged;
        _playerService.SourceChanged -= OnSourceChanged;
        _playerService.AudioTrackChanged -= OnSelectedTrackChanged;
        _playerService.SubtitleTrackChanged -= OnSelectedSubtitleChanged;
        StopTimer();
        CancelBufferingWatch();
        GC.SuppressFinalize(this);
    }
}
