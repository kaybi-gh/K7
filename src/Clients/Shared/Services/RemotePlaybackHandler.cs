using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Services;

public class RemotePlaybackHandler : IDisposable
{
    private readonly K7HubClient _hubClient;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IMediaService _mediaService;
    private readonly PlaybackProgressTracker _progressTracker;
    private readonly ISyncPlayMediaLoader _mediaLoader;
    private readonly IUiDispatcher _ui;
    private readonly RemotePlaybackLauncher _launcher;
    private readonly IExternalPlayerPolicy _externalPlayerPolicy;
    private readonly IWindowsMpcPlaybackHost? _mpcHost;
    private readonly ILogger<RemotePlaybackHandler> _logger;

    private Guid? _controllerDeviceId;
    private bool _isRemoteSession;
    private bool _hasEverPlayed;
    private double? _pausedHoldPosition;
    private Timer? _stateReportTimer;

    public RemotePlaybackHandler(
        K7HubClient hubClient,
        IPlayerService playerService,
        IAudioPlayerService audioPlayerService,
        IMediaService mediaService,
        PlaybackProgressTracker progressTracker,
        ISyncPlayMediaLoader mediaLoader,
        IUiDispatcher ui,
        RemotePlaybackLauncher launcher,
        IExternalPlayerPolicy externalPlayerPolicy,
        ILogger<RemotePlaybackHandler> logger,
        IWindowsMpcPlaybackHost? mpcHost = null)
    {
        _hubClient = hubClient;
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        _mediaService = mediaService;
        _progressTracker = progressTracker;
        _mediaLoader = mediaLoader;
        _ui = ui;
        _launcher = launcher;
        _externalPlayerPolicy = externalPlayerPolicy;
        _mpcHost = mpcHost;
        _logger = logger;

        _hubClient.RemotePlaybackRequested += OnRemotePlaybackRequested;
        _hubClient.RemoteTransportCommandReceived += OnRemoteTransportCommandReceived;
        _hubClient.PlaybackTakenOverReceived += OnPlaybackTakenOverReceived;
    }

    public bool HasPendingHandover { get; private set; }
    public PlaybackTakenOverDto? PendingHandover { get; private set; }
    public event Action? HandoverChanged;

    private void OnRemotePlaybackRequested(RemotePlaybackRequestDto request)
    {
        _ = HandlePlaybackRequestAsync(request);
    }

    private async Task HandlePlaybackRequestAsync(RemotePlaybackRequestDto request)
    {
        try
        {
            var media = await TryLoadMediaAsync(request.MediaId);
            await _ui.InvokeAsync(() => StartPlaybackOnUiAsync(request, media));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote playback request failed for file {IndexedFileId}", request.IndexedFileId);
            if (request.SenderDeviceId is Guid controllerId)
                await SendFinalStoppedStateAsync(controllerId);
        }
    }

    private async Task<MediaDto?> TryLoadMediaAsync(Guid? mediaId)
    {
        if (mediaId is not Guid id || id == Guid.Empty)
            return null;

        try
        {
            return await _mediaService.GetMediaAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load media {MediaId} for remote playback", id);
            return null;
        }
    }

    private async Task StartPlaybackOnUiAsync(RemotePlaybackRequestDto request, MediaDto? media)
    {
        _controllerDeviceId = request.SenderDeviceId;
        _isRemoteSession = true;
        _pausedHoldPosition = null;
        _hasEverPlayed = request.AttachOnly
            && (_playerService.PlaybackState == PlaybackState.Playing
                || _audioPlayerService.PlaybackState == PlaybackState.Playing);

        // MPC only polls position / seek-percent - cannot honor remote transport.
        if (_mpcHost?.IsActive == true)
        {
            if (request.AttachOnly)
            {
                _logger.LogInformation("Refusing remote attach while MPC external player is active");
                _isRemoteSession = false;
                _controllerDeviceId = null;
                if (request.SenderDeviceId is Guid controllerId)
                    await SendFinalStoppedStateAsync(controllerId);
                return;
            }

            await _mpcHost.StopAsync();
        }

        // Keep built-in player for the whole remote receive session (see ShouldUseMpc).
        _externalPlayerPolicy.SuppressExternalPlayer = true;

        if (request.AttachOnly)
        {
            StartStateReporting();
            return;
        }

        var indexedFile = ResolveIndexedFile(media, request.IndexedFileId);
        var title = request.Title ?? (media is not null ? VideoPlayerTitleHelper.FormatFromMedia(media) : null);

        if (request.IsAudio)
        {
            var duration = request.Duration;
            if (indexedFile?.FileMetadata is AudioFileMetadataDto audioMeta && audioMeta.Duration.TotalSeconds > 0)
                duration ??= audioMeta.Duration.TotalSeconds;

            var queueItem = new AudioQueueItem
            {
                IndexedFileId = request.IndexedFileId,
                MediaId = request.MediaId ?? media?.Id ?? Guid.Empty,
                Title = title ?? "Unknown",
                Artist = request.Artist ?? (media as MusicTrackDto)?.ArtistName,
                AlbumTitle = request.AlbumTitle ?? (media as MusicTrackDto)?.AlbumTitle,
                CoverUrl = request.CoverUrl,
                Duration = duration
            };

            await _audioPlayerService.PlayTrackAsync(queueItem);

            if (request.StartPosition is > 0)
            {
                _audioPlayerService.Seek(request.StartPosition.Value);
            }
        }
        else
        {
            var videoMetadata = indexedFile?.FileMetadata as VideoFileMetadataDto;
            var duration = request.Duration ?? videoMetadata?.Duration.TotalSeconds;
            var coverUrl = request.CoverUrl;
            var mediaId = request.MediaId ?? media?.Id;

            if (mediaId is Guid trackMediaId && trackMediaId != Guid.Empty)
            {
                var serieId = (media as SerieEpisodeDto)?.SerieId;
                _progressTracker.StartTracking(trackMediaId, isAuthenticated: true, serieId: serieId, indexedFileId: request.IndexedFileId);
            }

            await _playerService.PlayIndexedFileAsync(
                request.IndexedFileId,
                audioTracks: videoMetadata?.AudioTracks ?? [],
                subtitleTracks: videoMetadata?.SubtitleTracks,
                videoResolution: videoMetadata?.VideoResolution,
                thumbnailsUrl: videoMetadata?.Thumbnails?.Uri?.OriginalString
                    ?? videoMetadata?.Thumbnails?.Uri?.ToString(),
                mediaId: mediaId,
                title: title,
                coverUrl: coverUrl,
                startPosition: request.StartPosition is > 0 ? request.StartPosition : null,
                chapters: videoMetadata?.Chapters,
                durationSeconds: duration,
                libraryId: indexedFile?.LibraryId,
                filePath: indexedFile?.Path);

            if (request.Volume is double volume)
                _playerService.SetVolume(Math.Clamp(volume, 0, 1));
        }

        StartStateReporting();
    }

    private static IndexedFileDto? ResolveIndexedFile(MediaDto? media, Guid indexedFileId)
    {
        if (media?.IndexedFiles is null || media.IndexedFiles.Count == 0)
            return null;

        return media.IndexedFiles.FirstOrDefault(f => f.Id == indexedFileId)
            ?? media.IndexedFiles.FirstOrDefault();
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
        _pausedHoldPosition = null;
        _controllerDeviceId = null;
        _externalPlayerPolicy.SuppressExternalPlayer = false;
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
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report stopped remote playback state");
        }
    }

    private async Task ReportStateAsync()
    {
        if (_controllerDeviceId is null || !_isRemoteSession) return;

        var isVideoActive = _playerService.IsVisible;
        var state = BuildStateDto(isVideoActive);

        if (state.State == RemotePlaybackState.Playing)
        {
            _hasEverPlayed = true;
            _pausedHoldPosition = null;
        }
        else if (state.State == RemotePlaybackState.Stopped && !_hasEverPlayed)
        {
            state = state with { State = RemotePlaybackState.Buffering };
        }

        try
        {
            await _hubClient.ReportRemotePlaybackStateAsync(_controllerDeviceId.Value, state);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report remote playback state");
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
            var position = _pausedHoldPosition ?? _playerService.CurrentTime;
            return new RemotePlaybackStateDto
            {
                State = ToRemoteState(_playerService.PlaybackState, mediaShellVisible: true),
                Position = position,
                Duration = _playerService.Duration,
                Volume = _playerService.Volume,
                SelectedAudioTrackIndex = _playerService.SelectedAudioTrack?.Index,
                SelectedSubtitleTrackIndex = _playerService.SelectedSubtitleTrack?.Index,
                AudioTracks = _playerService.AudioTracks.Select(t => new RemoteTrackInfoDto
                {
                    Index = t.Index,
                    Label = AudioTrackDisplayHelper.FormatLabel(t),
                    Language = t.Language,
                    Name = t.Name,
                    Codec = t.Codec,
                    ChannelLayout = t.ChannelLayout
                }).ToList(),
                SubtitleTracks = _playerService.SubtitleTracks.Select(t => new RemoteTrackInfoDto
                {
                    Index = t.Index,
                    Label = t.Name ?? t.Language ?? $"Subtitle {t.Index}",
                    Language = t.Language,
                    Name = t.Name,
                    Codec = t.Codec,
                    IsForced = t.IsForced,
                    IsHearingImpaired = t.IsHearingImpaired
                }).ToList(),
                ThumbnailsUrl = _playerService.Source?.ThumbnailsUrl,
                Chapters = _playerService.Source?.Chapters,
                PlaybackRate = _playerService.PlaybackRate,
                AspectRatio = (int)_playerService.AspectRatio
            };
        }

        return new RemotePlaybackStateDto
        {
            State = ToRemoteState(_audioPlayerService.PlaybackState, mediaShellVisible: _audioPlayerService.IsVisible),
            Position = _pausedHoldPosition ?? _audioPlayerService.CurrentTime,
            Duration = _audioPlayerService.Duration,
            Volume = _audioPlayerService.Volume
        };
    }

    private static RemotePlaybackState ToRemoteState(PlaybackState state, bool mediaShellVisible) => state switch
    {
        PlaybackState.Playing => RemotePlaybackState.Playing,
        PlaybackState.Paused => RemotePlaybackState.Paused,
        PlaybackState.Buffering => RemotePlaybackState.Buffering,
        // Visible shell with Idle is usually a track reload, not a real stop.
        _ when mediaShellVisible => RemotePlaybackState.Buffering,
        _ => RemotePlaybackState.Stopped
    };

    private void OnRemoteTransportCommandReceived(RemoteTransportCommandDto command)
    {
        _ = _ui.InvokeAsync(() => ApplyTransportCommandAsync(command));
    }

    private async Task ApplyTransportCommandAsync(RemoteTransportCommandDto command)
    {
        var isVideoActive = _playerService.IsVisible;

        switch (command.Action)
        {
            case RemoteTransportAction.Play:
                _pausedHoldPosition = null;
                if (isVideoActive)
                    _playerService.Play();
                else
                    _audioPlayerService.Play();
                break;

            case RemoteTransportAction.Pause:
                _pausedHoldPosition = isVideoActive
                    ? _playerService.GetResumePosition()
                    : _audioPlayerService.CurrentTime;
                if (isVideoActive)
                    _playerService.Pause();
                else
                    _audioPlayerService.Pause();
                break;

            case RemoteTransportAction.Stop:
                var stoppedControllerId = _controllerDeviceId;
                StopStateReporting();
                await TearDownLocalPlaybackAsync();
                if (stoppedControllerId is not null)
                    await SendFinalStoppedStateAsync(stoppedControllerId.Value);
                break;

            case RemoteTransportAction.ReleaseControl:
                StopStateReporting();
                break;

            case RemoteTransportAction.SeekTo:
                if (command.Value is double seekTime)
                {
                    _pausedHoldPosition = null;
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
                    var audioTrack = _playerService.AudioTracks.FirstOrDefault(t => t.Index == audioIdx)
                        ?? _playerService.AudioTracks
                            .OrderBy(t => t.Index)
                            .ElementAtOrDefault(audioIdx);
                    if (audioTrack is not null)
                    {
                        var keepRate = _playerService.PlaybackRate;
                        await _playerService.ChangeAudioTrackAsync(audioTrack);
                        // HLS reload resets decoder rate to 1 - restore or A/V + subs drift.
                        if (keepRate > 0 && Math.Abs(keepRate - 1) > 0.01)
                            _playerService.SetPlaybackRate(keepRate);
                    }
                }
                break;

            case RemoteTransportAction.SetSubtitleTrack:
                if (command.TrackIndex is int subIdx && isVideoActive)
                {
                    var subTrack = _playerService.SubtitleTracks.FirstOrDefault(t => t.Index == subIdx);
                    var keepSubRate = _playerService.PlaybackRate;
                    await _playerService.ChangeSubtitleTrackAsync(subTrack);
                    if (keepSubRate > 0 && Math.Abs(keepSubRate - 1) > 0.01)
                        _playerService.SetPlaybackRate(keepSubRate);
                }
                break;

            case RemoteTransportAction.SetPlaybackRate:
                if (command.Value is double rate && isVideoActive)
                    _playerService.SetPlaybackRate(rate);
                break;

            case RemoteTransportAction.SetAspectRatio:
                if (command.Value is double aspectValue && isVideoActive)
                {
                    var aspectInt = (int)Math.Round(aspectValue);
                    if (Enum.IsDefined(typeof(AspectRatioMode), aspectInt))
                        _playerService.SetAspectRatioMode((AspectRatioMode)aspectInt);
                }
                break;
        }
    }

    private void OnPlaybackTakenOverReceived(PlaybackTakenOverDto dto)
    {
        _ = _ui.InvokeAsync(() => OfferHandoverAsync(dto));
    }

    private async Task OfferHandoverAsync(PlaybackTakenOverDto dto)
    {
        if (!MatchesLocalPlayback(dto))
            return;

        // Flag first so native chrome drops immediately (WebView must paint the dialog).
        PendingHandover = dto;
        HasPendingHandover = true;
        HandoverChanged?.Invoke();
        await TearDownLocalPlaybackAsync();
    }

    private bool MatchesLocalPlayback(PlaybackTakenOverDto dto)
    {
        var videoActive = _playerService.IsVisible;
        var audioActive = _audioPlayerService.IsVisible
            || _audioPlayerService.PlaybackState is PlaybackState.Playing or PlaybackState.Paused or PlaybackState.Buffering;

        if (!videoActive && !audioActive)
            return false;

        if (dto.IndexedFileId is Guid fileId)
        {
            if (videoActive && _playerService.Source?.IndexedFileId == fileId)
                return true;
            if (audioActive && _audioPlayerService.CurrentTrack?.IndexedFileId == fileId)
                return true;
        }

        if (dto.MediaId is Guid mediaId)
        {
            if (videoActive && _playerService.Source?.MediaId == mediaId)
                return true;
            if (audioActive && _audioPlayerService.CurrentTrack?.MediaId == mediaId)
                return true;
        }

        // Takeover without ids still stops whatever is playing locally.
        return dto.IndexedFileId is null && dto.MediaId is null;
    }

    public async Task ConfirmHandoverDismissAsync()
    {
        ClearHandover();
        await TearDownLocalPlaybackAsync();
    }

    public async Task ConfirmHandoverRemoteAsync()
    {
        var dto = PendingHandover;
        ClearHandover();
        await TearDownLocalPlaybackAsync();
        if (dto is null || dto.NewDeviceId == Guid.Empty)
            return;

        await _launcher.AttachToDeviceAsync(new NowPlayingSessionDto
        {
            DeviceId = dto.NewDeviceId,
            DeviceName = dto.NewDeviceName,
            IndexedFileId = dto.IndexedFileId,
            MediaId = dto.MediaId,
            MediaTitle = dto.Title,
            ThumbnailUrl = dto.CoverUrl,
            Position = dto.Position,
            Duration = dto.Duration,
            IsAudio = dto.IsAudio,
            CanControl = true
        });
    }

    public async Task ConfirmHandoverResumeAsync()
    {
        var dto = PendingHandover;
        ClearHandover();
        if (dto is { NewDeviceId: var deviceId } && deviceId != Guid.Empty)
            _ = _launcher.StopOnDeviceAsync(deviceId);

        if (dto?.MediaId is not Guid mediaId)
            return;

        await _mediaLoader.LoadAndPlayMediaAsync(
            mediaId,
            dto.Title,
            dto.CoverUrl,
            dto.Position > 1 ? dto.Position : null);
    }

    public void DiscardPendingHandover()
    {
        if (!HasPendingHandover)
            return;

        ClearHandover();
    }

    public Task StopLocalPlaybackQuietlyAsync() => TearDownLocalPlaybackAsync();

    private void ClearHandover()
    {
        HasPendingHandover = false;
        PendingHandover = null;
        HandoverChanged?.Invoke();
    }

    private async Task TearDownLocalPlaybackAsync()
    {
        try
        {
            if (_playerService.IsVisible)
            {
                try
                {
                    _playerService.Pause();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Pause during handover teardown failed");
                }

                await _playerService.HideAsync();

                // Do not call Stop() here. On Windows direct/LibVLC, Stop during takeover
                // can freeze the UI thread and block the peer Resume-here flow.
            }

            if (_audioPlayerService.PlaybackState is PlaybackState.Playing or PlaybackState.Paused or PlaybackState.Buffering)
                _audioPlayerService.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to tear down local playback");
        }
    }

    public void Dispose()
    {
        StopStateReporting();
        _hubClient.RemotePlaybackRequested -= OnRemotePlaybackRequested;
        _hubClient.RemoteTransportCommandReceived -= OnRemoteTransportCommandReceived;
        _hubClient.PlaybackTakenOverReceived -= OnPlaybackTakenOverReceived;
    }
}
