using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Tracks playback progress and periodically reports it to the server.
/// Hooks into IPlayerService events to detect time updates, pause, and stop.
/// Skips reporting when the user is not authenticated (guest mode).
/// </summary>
public class PlaybackProgressTracker : IDisposable
{
    private readonly IPlayerService _playerService;
    private readonly IStreamingService _serverService;
    private readonly IDeviceStorageService _deviceStorage;
    private readonly IConnectivityService _connectivity;
    private readonly IPlaybackJournal _journal;
    private Timer? _reportTimer;
    private Guid? _currentMediaId;
    private Guid? _currentSerieId;
    private Guid _sessionId;
    private Guid _referenceId;
    private Guid? _currentIndexedFileId;
    private double _lastReportedPosition;
    private double _lastKnownTime;
    private bool _isAuthenticated;
    private bool _disposed;
    private PlaybackState _lastState = PlaybackState.Unknown;

    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(10);
    private const double MinPositionDeltaToReport = 2.0;
    private const double SeekDetectionThreshold = 3.0;

    public Guid? CurrentMediaId => _currentMediaId;
    public Guid? CurrentSerieId => _currentSerieId;

    public PlaybackProgressTracker(
        IPlayerService playerService,
        IStreamingService serverService,
        IDeviceStorageService deviceStorage,
        IConnectivityService connectivity,
        IPlaybackJournal journal)
    {
        _playerService = playerService;
        _serverService = serverService;
        _deviceStorage = deviceStorage;
        _connectivity = connectivity;
        _journal = journal;

        _playerService.PlaybackStateChanged += OnPlaybackStateChanged;
        _playerService.CurrentTimeChanged += OnCurrentTimeChanged;
    }

    /// <summary>
    /// Begins tracking a specific media. Call this when a new media starts playing.
    /// </summary>
    /// <param name="mediaId">The media being played.</param>
    /// <param name="isAuthenticated">Whether the current user is authenticated. When false, progress is not reported.</param>
    /// <param name="serieId">Optional serie ID when playing a serie episode.</param>
    public void StartTracking(Guid mediaId, bool isAuthenticated = true, Guid? serieId = null, Guid? indexedFileId = null)
    {
        StopTimer();
        _currentMediaId = mediaId;
        _currentSerieId = serieId;
        _currentIndexedFileId = indexedFileId;
        _sessionId = Guid.NewGuid();
        _referenceId = Guid.NewGuid();
        _lastReportedPosition = 0;
        _isAuthenticated = isAuthenticated;
        StartTimer();
    }

    /// <summary>
    /// Stops tracking and sends a final progress report.
    /// </summary>
    public async void StopTracking()
    {
        StopTimer();
        var mediaId = _currentMediaId;
        _currentMediaId = null;
        _currentSerieId = null;
        if (mediaId is not null)
        {
            await ReportProgressAsync(mediaId.Value);
        }
    }

    private void OnCurrentTimeChanged(double time)
    {
        // Detect significant seek (forward or backward) and immediately report
        if (_currentMediaId is not null && Math.Abs(time - _lastKnownTime) > SeekDetectionThreshold)
        {
            _ = ReportProgressAsync();
        }

        _lastKnownTime = time;
    }

    private void OnPlaybackStateChanged(PlaybackState state)
    {
        _lastState = state;
        switch (state)
        {
            case PlaybackState.Paused:
                StopTimer();
                _ = ReportProgressAsync();
                break;
            case PlaybackState.Playing:
                _ = ReportProgressAsync();
                StartTimer();
                break;
            case PlaybackState.Idle:
                StopTimer();
                _ = ReportProgressAsync();
                break;
        }
    }

    private void StartTimer()
    {
        _reportTimer ??= new Timer(_ => _ = ReportProgressAsync(), null, ReportInterval, ReportInterval);
    }

    private void StopTimer()
    {
        _reportTimer?.Dispose();
        _reportTimer = null;
    }

    private async Task ReportProgressAsync() => await ReportProgressAsync(_currentMediaId);

    private async Task ReportProgressAsync(Guid? mediaId)
    {
        if (mediaId is null) return;
        if (!_isAuthenticated) return;

        var position = _playerService.CurrentTime;
        var duration = _playerService.Duration;

        if (duration <= 0) return;
        if (Math.Abs(position - _lastReportedPosition) < MinPositionDeltaToReport) return;

        _lastReportedPosition = position;

        if (!_connectivity.IsOnline && _currentIndexedFileId.HasValue)
        {
            await _journal.RecordProgressAsync(mediaId.Value, _currentIndexedFileId.Value, position, duration);
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
                (int)_lastState,
                deviceId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to report playback progress: {ex.Message}");
            if (_currentIndexedFileId.HasValue)
            {
                await _journal.RecordProgressAsync(mediaId.Value, _currentIndexedFileId.Value, position, duration);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _playerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _playerService.CurrentTimeChanged -= OnCurrentTimeChanged;
        StopTimer();
        GC.SuppressFinalize(this);
    }
}
