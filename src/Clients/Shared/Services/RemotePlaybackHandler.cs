using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Services;

public class RemotePlaybackHandler : IDisposable
{
    private readonly K7HubClient _hubClient;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;

    private Guid? _controllerDeviceId;
    private bool _isRemoteSession;
    private bool _hasEverPlayed;
    private Timer? _stateReportTimer;

    public RemotePlaybackHandler(
        K7HubClient hubClient,
        IPlayerService playerService,
        IAudioPlayerService audioPlayerService)
    {
        _hubClient = hubClient;
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;

        _hubClient.RemotePlaybackRequested += OnRemotePlaybackRequested;
        _hubClient.RemoteTransportCommandReceived += OnRemoteTransportCommandReceived;
    }

    private void OnRemotePlaybackRequested(RemotePlaybackRequestDto request)
    {
        _ = HandlePlaybackRequestAsync(request);
    }

    private async Task HandlePlaybackRequestAsync(RemotePlaybackRequestDto request)
    {
        _controllerDeviceId = request.SenderDeviceId;
        _isRemoteSession = true;
        _hasEverPlayed = false;

        if (request.IsAudio)
        {
            var queueItem = new AudioQueueItem
            {
                IndexedFileId = request.IndexedFileId,
                MediaId = request.MediaId ?? Guid.Empty,
                Title = request.Title ?? "Unknown",
                Artist = request.Artist,
                AlbumTitle = request.AlbumTitle,
                CoverUrl = request.CoverUrl,
                Duration = request.Duration
            };

            await _audioPlayerService.PlayTrackAsync(queueItem);

            if (request.StartPosition is > 0)
            {
                _audioPlayerService.Seek(request.StartPosition.Value);
            }
        }
        else
        {
            await _playerService.PlayIndexedFileAsync(
                request.IndexedFileId,
                audioTracks: []);

            if (request.StartPosition is > 0)
            {
                _playerService.Seek(request.StartPosition.Value);
            }
        }

        StartStateReporting();
    }

    private void StartStateReporting()
    {
        _stateReportTimer?.Dispose();
        _stateReportTimer = new Timer(_ => _ = ReportStateAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void StopStateReporting()
    {
        _stateReportTimer?.Dispose();
        _stateReportTimer = null;
        _isRemoteSession = false;
        _hasEverPlayed = false;
        _controllerDeviceId = null;
    }

    private async Task SendFinalStoppedStateAsync(Guid controllerDeviceId)
    {
        var state = new RemotePlaybackStateDto
        {
            State = RemotePlaybackState.Stopped,
            Position = 0,
            Duration = 0,
            Volume = 0
        };

        try
        {
            await _hubClient.ReportRemotePlaybackStateAsync(controllerDeviceId, state);
        }
        catch { }
    }

    private async Task ReportStateAsync()
    {
        if (_controllerDeviceId is null || !_isRemoteSession) return;

        var isVideoActive = _playerService.IsVisible;
        var state = BuildStateDto(isVideoActive);

        if (state.State == RemotePlaybackState.Playing)
        {
            _hasEverPlayed = true;
        }
        else if (state.State == RemotePlaybackState.Stopped && !_hasEverPlayed)
        {
            // Playback never started (e.g. autoplay blocked) - report as Buffering
            // to avoid ending the sender's remote session prematurely
            state = state with { State = RemotePlaybackState.Buffering };
        }

        try
        {
            await _hubClient.ReportRemotePlaybackStateAsync(_controllerDeviceId.Value, state);
        }
        catch
        {
            // Ignore send failures (disconnection, etc.)
        }

        if (state.State == RemotePlaybackState.Stopped)
        {
            StopStateReporting();
        }
    }

    private RemotePlaybackStateDto BuildStateDto(bool isVideoActive)
    {
        if (isVideoActive)
        {
            return new RemotePlaybackStateDto
            {
                State = ToRemoteState(_playerService.PlaybackState),
                Position = _playerService.CurrentTime,
                Duration = _playerService.Duration,
                Volume = _playerService.Volume,
                SelectedAudioTrackIndex = _playerService.SelectedAudioTrack?.Index,
                SelectedSubtitleTrackIndex = _playerService.SelectedSubtitleTrack?.Index,
                AudioTracks = _playerService.AudioTracks.Select(t => new RemoteTrackInfoDto
                {
                    Index = t.Index,
                    Label = t.Name ?? t.Language ?? $"Track {t.Index}"
                }).ToList(),
                SubtitleTracks = _playerService.SubtitleTracks.Select(t => new RemoteTrackInfoDto
                {
                    Index = t.Index,
                    Label = t.Name ?? t.Language ?? $"Subtitle {t.Index}"
                }).ToList()
            };
        }

        return new RemotePlaybackStateDto
        {
            State = ToRemoteState(_audioPlayerService.PlaybackState),
            Position = _audioPlayerService.CurrentTime,
            Duration = _audioPlayerService.Duration,
            Volume = _audioPlayerService.Volume
        };
    }

    private static RemotePlaybackState ToRemoteState(PlaybackState state) => state switch
    {
        PlaybackState.Playing => RemotePlaybackState.Playing,
        PlaybackState.Paused => RemotePlaybackState.Paused,
        PlaybackState.Buffering => RemotePlaybackState.Buffering,
        _ => RemotePlaybackState.Stopped
    };

    private void OnRemoteTransportCommandReceived(RemoteTransportCommandDto command)
    {
        var isVideoActive = _playerService.IsVisible;

        switch (command.Action)
        {
            case RemoteTransportAction.Play:
                if (isVideoActive)
                    _playerService.Play();
                else
                    _audioPlayerService.Play();
                break;

            case RemoteTransportAction.Pause:
                if (isVideoActive)
                    _playerService.Pause();
                else
                    _audioPlayerService.Pause();
                break;

            case RemoteTransportAction.Stop:
                var stoppedControllerId = _controllerDeviceId;
                if (isVideoActive)
                    _playerService.Stop();
                else
                    _audioPlayerService.Stop();
                StopStateReporting();
                if (stoppedControllerId is not null)
                    _ = SendFinalStoppedStateAsync(stoppedControllerId.Value);
                break;

            case RemoteTransportAction.SeekTo:
                if (command.Value is double seekTime)
                {
                    if (isVideoActive)
                        _playerService.Seek(seekTime);
                    else
                        _audioPlayerService.Seek(seekTime);
                }
                break;

            case RemoteTransportAction.SetVolume:
                if (command.Value is double volume)
                {
                    if (isVideoActive)
                        _playerService.SetVolume(volume);
                    else
                        _audioPlayerService.SetVolume(volume);
                }
                break;

            case RemoteTransportAction.SetAudioTrack:
                if (command.TrackIndex is int audioIdx && isVideoActive)
                {
                    var audioTrack = _playerService.AudioTracks.FirstOrDefault(t => t.Index == audioIdx);
                    if (audioTrack is not null)
                        _ = _playerService.ChangeAudioTrackAsync(audioTrack);
                }
                break;

            case RemoteTransportAction.SetSubtitleTrack:
                if (command.TrackIndex is int subIdx && isVideoActive)
                {
                    var subTrack = _playerService.SubtitleTracks.FirstOrDefault(t => t.Index == subIdx);
                    _ = _playerService.ChangeSubtitleTrackAsync(subTrack);
                }
                break;
        }
    }

    public void Dispose()
    {
        StopStateReporting();
        _hubClient.RemotePlaybackRequested -= OnRemotePlaybackRequested;
        _hubClient.RemoteTransportCommandReceived -= OnRemoteTransportCommandReceived;
    }
}
