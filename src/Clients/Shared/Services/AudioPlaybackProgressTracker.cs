using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class AudioPlaybackProgressTracker : IDisposable
{
    private readonly IAudioPlayerService _audio;
    private readonly IK7ServerService _serverService;
    private Timer? _reportTimer;
    private Guid? _currentMediaId;
    private Guid _sessionId;
    private bool _disposed;
    private bool _transitioning;

    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(30);

    public AudioPlaybackProgressTracker(IAudioPlayerService audio, IK7ServerService serverService)
    {
        _audio = audio;
        _serverService = serverService;

        _audio.CurrentTrackChanged += OnTrackChanged;
        _audio.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    private void OnTrackChanged(AudioQueueItem? newTrack)
    {
        // Capture old track values synchronously (before the service resets them)
        var prevMediaId = _currentMediaId;
        var prevSessionId = _sessionId;
        var prevPosition = _audio.CurrentTime;
        var prevDuration = _audio.Duration;

        StopTimer();

        if (prevMediaId.HasValue && prevDuration > 0)
            _ = SendReportAsync(prevMediaId.Value, prevSessionId, prevPosition, prevDuration);

        _currentMediaId = newTrack?.MediaId;

        if (newTrack is not null)
        {
            _sessionId = Guid.NewGuid();
            _transitioning = true;
            StartTimer();
        }
    }

    private void OnPlaybackStateChanged(PlaybackState state)
    {
        switch (state)
        {
            case PlaybackState.Playing:
                _transitioning = false;
                StartTimer();
                break;
            case PlaybackState.Paused:
            case PlaybackState.Idle:
            case PlaybackState.Ended:
                StopTimer();
                if (!_transitioning)
                    _ = ReportCurrentAsync();
                break;
        }
    }

    private async Task ReportCurrentAsync()
    {
        if (_currentMediaId is not { } mediaId) return;

        var position = _audio.CurrentTime;
        var duration = _audio.Duration;
        if (duration <= 0) return;

        await SendReportAsync(mediaId, _sessionId, position, duration);
    }

    private async Task SendReportAsync(Guid mediaId, Guid sessionId, double position, double duration)
    {
        try
        {
            await _serverService.ReportPlaybackProgressAsync(mediaId, sessionId, position, duration);
        }
        catch
        {
            // Silent — fire and forget
        }
    }

    private void StartTimer()
    {
        _reportTimer ??= new Timer(_ => _ = ReportCurrentAsync(), null, ReportInterval, ReportInterval);
    }

    private void StopTimer()
    {
        _reportTimer?.Dispose();
        _reportTimer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _audio.CurrentTrackChanged -= OnTrackChanged;
        _audio.PlaybackStateChanged -= OnPlaybackStateChanged;

        StopTimer();
        GC.SuppressFinalize(this);
    }
}
