using K7.Clients.Shared.Domain.Interfaces;
using K7.Server.Domain.Enums;
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
    private readonly IK7ServerService _serverService;
    private Timer? _reportTimer;
    private Guid? _currentMediaId;
    private Guid _sessionId;
    private double _lastReportedPosition;
    private double _lastKnownTime;
    private bool _isAuthenticated;
    private bool _disposed;

    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(10);
    private const double MinPositionDeltaToReport = 2.0;
    private const double SeekDetectionThreshold = 3.0;

    public PlaybackProgressTracker(IPlayerService playerService, IK7ServerService serverService)
    {
        _playerService = playerService;
        _serverService = serverService;

        _playerService.PlaybackStateChanged += OnPlaybackStateChanged;
        _playerService.CurrentTimeChanged += OnCurrentTimeChanged;
    }

    /// <summary>
    /// Begins tracking a specific media. Call this when a new media starts playing.
    /// </summary>
    /// <param name="mediaId">The media being played.</param>
    /// <param name="isAuthenticated">Whether the current user is authenticated. When false, progress is not reported.</param>
    public void StartTracking(Guid mediaId, bool isAuthenticated = true)
    {
        StopTimer();
        _currentMediaId = mediaId;
        _sessionId = Guid.NewGuid();
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
        switch (state)
        {
            case PlaybackState.Paused:
                StopTimer();
                _ = ReportProgressAsync();
                break;
            case PlaybackState.Playing:
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

        try
        {
            await _serverService.ReportPlaybackProgressAsync(
                mediaId.Value,
                _sessionId,
                position,
                duration);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to report playback progress: {ex.Message}");
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
