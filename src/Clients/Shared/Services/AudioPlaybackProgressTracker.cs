using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class AudioPlaybackProgressTracker : IDisposable
{
    private readonly IAudioPlayerService _audio;
    private readonly IStreamingService _serverService;
    private readonly IDeviceStorageService _deviceStorage;
    private readonly IConnectivityService _connectivity;
    private readonly IPlaybackJournal _journal;
    private readonly ISyncPlayService? _syncPlayService;
    private Timer? _reportTimer;
    private Guid? _currentMediaId;
    private Guid? _currentIndexedFileId;
    private Guid _sessionId;
    private Guid _referenceId;
    private bool _disposed;
    private bool _transitioning;
    private bool _canReport;
    private PlaybackState _lastState = PlaybackState.Unknown;

    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(3);

    public AudioPlaybackProgressTracker(
        IAudioPlayerService audio,
        IStreamingService serverService,
        IDeviceStorageService deviceStorage,
        IConnectivityService connectivity,
        IPlaybackJournal journal,
        ISyncPlayService? syncPlayService = null)
    {
        _audio = audio;
        _serverService = serverService;
        _deviceStorage = deviceStorage;
        _connectivity = connectivity;
        _journal = journal;
        _syncPlayService = syncPlayService;

        _audio.CurrentTrackChanged += OnTrackChanged;
        _audio.PlaybackStateChanged += OnPlaybackStateChanged;
        _audio.SourceChanged += OnSourceChanged;
    }

    public void SetCanReport(bool value) => _canReport = value;

    private void OnTrackChanged(AudioQueueItem? newTrack)
    {
        // Capture old track values synchronously (before the service resets them)
        var prevMediaId = _currentMediaId;
        var prevIndexedFileId = _currentIndexedFileId;
        var prevSessionId = _sessionId;
        var prevPosition = _audio.CurrentTime;
        var prevDuration = _audio.Duration;

        StopTimer();

        if (prevMediaId.HasValue && prevDuration > 0)
            _ = SendReportAsync(prevMediaId.Value, prevIndexedFileId, prevSessionId, prevPosition, prevDuration, PlaybackState.Ended);

        _currentMediaId = newTrack?.MediaId;
        _currentIndexedFileId = newTrack?.IndexedFileId;

        if (newTrack is not null)
        {
            _sessionId = Guid.NewGuid();
            _referenceId = Guid.NewGuid();
            _transitioning = true;
            StartTimer();
        }
    }

    private void OnSourceChanged(PlayerSource source)
    {
        // Use the server's StreamSessionId so progress reports match the stream tracker
        if (source.StreamSessionId is { } serverSessionId)
        {
            _sessionId = serverSessionId;
        }
    }

    private void OnPlaybackStateChanged(PlaybackState state)
    {
        _lastState = state;
        switch (state)
        {
            case PlaybackState.Playing:
                _transitioning = false;
                _ = ReportCurrentAsync();
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

        await SendReportAsync(mediaId, _currentIndexedFileId, _sessionId, position, duration, _lastState);
    }

    private async Task SendReportAsync(Guid mediaId, Guid? indexedFileId, Guid sessionId, double position, double duration, PlaybackState state)
    {
        if (!_canReport) return;

        if (!_connectivity.IsOnline && indexedFileId.HasValue)
        {
            if (state == PlaybackState.Ended)
                await _journal.RecordCompletedAsync(mediaId, indexedFileId.Value, duration);
            else
                await _journal.RecordProgressAsync(mediaId, indexedFileId.Value, position, duration);
            return;
        }

        try
        {
            var deviceIdStr = _deviceStorage.Get(PreferenceKeys.DEVICE_ID);
            Guid? deviceId = Guid.TryParse(deviceIdStr, out var parsed) ? parsed : null;
            await _serverService.ReportPlaybackProgressAsync(
                mediaId,
                sessionId,
                _referenceId,
                position,
                duration,
                (int)state,
                deviceId,
                _audio.ActivePlaylistId,
                syncPlayGroupId: _syncPlayService?.IsInGroup == true ? _syncPlayService.CurrentGroup?.GroupId : null);
        }
        catch
        {
            if (indexedFileId.HasValue)
            {
                if (state == PlaybackState.Ended)
                    await _journal.RecordCompletedAsync(mediaId, indexedFileId.Value, duration);
                else
                    await _journal.RecordProgressAsync(mediaId, indexedFileId.Value, position, duration);
            }
        }
    }

    private void StartTimer()
    {
        _reportTimer ??= new Timer(_ => _ = ReportCurrentAsync(), null, InitialDelay, ReportInterval);
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
        _audio.SourceChanged -= OnSourceChanged;

        StopTimer();
        GC.SuppressFinalize(this);
    }
}
