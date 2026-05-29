using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class CastOrchestrationService : ICastOrchestrationService
{
    private readonly ICastService _castService;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IStreamingService _streamingService;
    private readonly IStreamUriService _streamUriService;
    private readonly IK7ServerService _k7ServerService;
    private readonly IDeviceStorageService _deviceStorageService;
    private readonly IRemoteControlService _remoteControlService;

    private Guid? _activeStreamSessionId;

    public CastOrchestrationService(
        ICastService castService,
        IPlayerService playerService,
        IAudioPlayerService audioPlayerService,
        IStreamingService streamingService,
        IStreamUriService streamUriService,
        IK7ServerService k7ServerService,
        IDeviceStorageService deviceStorageService,
        IRemoteControlService remoteControlService)
    {
        _castService = castService;
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        _streamingService = streamingService;
        _streamUriService = streamUriService;
        _k7ServerService = k7ServerService;
        _deviceStorageService = deviceStorageService;
        _remoteControlService = remoteControlService;
    }

    public async Task CastCurrentVideoAsync(CastDeviceInfo device, CancellationToken cancellationToken = default)
    {
        var source = _playerService.Source;
        if (source?.StreamSessionId is null || source.Url is null) return;

        var token = await _streamingService.GenerateEphemeralTokenAsync(source.StreamSessionId.Value, cancellationToken);
        if (token is null) return;

        _activeStreamSessionId = source.StreamSessionId.Value;

        var castUrl = AppendEphemeralToken(source.Url, token);

        _playerService.Pause();

        await _castService.CastAsync(new CastMediaRequest
        {
            StreamUrl = castUrl,
            ContentType = source.MimeType ?? "application/x-mpegURL",
            Title = source.Title,
            ThumbnailUrl = source.CoverUrl is not null
                ? _k7ServerService.GetAbsoluteUri(source.CoverUrl)?.ToString()
                : null,
            Duration = _playerService.Duration,
            StartPosition = _playerService.CurrentTime
        });

        _remoteControlService.StartCastSession("Chromecast", false, source.Title, null, null, source.CoverUrl, _playerService.Duration, _playerService.CurrentTime);
    }

    public async Task CastCurrentAudioAsync(CastDeviceInfo device, CancellationToken cancellationToken = default)
    {
        var track = _audioPlayerService.CurrentTrack;
        if (track is null) return;

        var storedDeviceId = _deviceStorageService.Get(PreferenceKeys.DEVICE_ID);
        if (string.IsNullOrWhiteSpace(storedDeviceId)) return;

        var session = await _streamUriService.GetOrCreateSessionAsync(track.IndexedFileId, cancellationToken: cancellationToken);
        if (session.Source is null) return;

        var token = await _streamingService.GenerateEphemeralTokenAsync(session.Id, cancellationToken);
        if (token is null) return;

        _activeStreamSessionId = session.Id;

        var castUrl = AppendEphemeralToken(session.Source.Uri.OriginalString, token);

        _audioPlayerService.Pause();

        await _castService.CastAsync(new CastMediaRequest
        {
            StreamUrl = castUrl,
            ContentType = session.Source.MimeType ?? "audio/mpeg",
            Title = track.Title,
            Subtitle = track.Artist,
            ThumbnailUrl = track.CoverUrl is not null
                ? _k7ServerService.GetAbsoluteUri(track.CoverUrl)?.ToString()
                : null,
            Duration = _audioPlayerService.Duration,
            StartPosition = _audioPlayerService.CurrentTime
        });

        _remoteControlService.StartCastSession("Chromecast", true, track.Title, track.Artist, track.AlbumTitle, track.CoverUrl, _audioPlayerService.Duration, _audioPlayerService.CurrentTime);
    }

    public async Task StopCastingAsync(CancellationToken cancellationToken = default)
    {
        await _castService.StopCastingAsync();

        if (_activeStreamSessionId is not null)
        {
            await _streamingService.RevokeEphemeralTokenAsync(_activeStreamSessionId.Value, cancellationToken);
            _activeStreamSessionId = null;
        }
    }

    private static string AppendEphemeralToken(string url, string token)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}ephemeral_token={Uri.EscapeDataString(token)}";
    }
}
