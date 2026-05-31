using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class StreamUriService : IStreamUriService
{
    private readonly IStreamingService _streamingService;
    private readonly IK7ServerService _k7ServerService;
    private readonly IDeviceStorageService _deviceStorageService;
    private readonly IOfflineMediaStore? _offlineStore;
    private readonly IMusicCacheService? _musicCache;

    public StreamUriService(
        IStreamingService streamingService,
        IK7ServerService k7ServerService,
        IDeviceStorageService deviceStorageService,
        IOfflineMediaStore? offlineStore = null,
        IMusicCacheService? musicCache = null)
    {
        _streamingService = streamingService;
        _k7ServerService = k7ServerService;
        _deviceStorageService = deviceStorageService;
        _offlineStore = offlineStore;
        _musicCache = musicCache;
    }

    public async Task<StreamingSessionDto> GetOrCreateSessionAsync(Guid indexedFileId, int? audioTrackIndex = null, CancellationToken cancellationToken = default)
    {
        // Check offline store first (explicit downloads)
        if (_offlineStore is not null)
        {
            var offlineItem = await _offlineStore.GetByIndexedFileIdAsync(indexedFileId, cancellationToken);
            if (offlineItem is not null && File.Exists(offlineItem.MediaLocalPath))
            {
                return CreateOfflineSession(indexedFileId, offlineItem.MediaLocalPath);
            }
        }

        // Check music cache (lookahead cached tracks)
        if (_musicCache is not null)
        {
            var cachedPath = await _musicCache.GetCachedTrackPathAsync(indexedFileId, cancellationToken);
            if (cachedPath is not null && File.Exists(cachedPath))
            {
                return CreateOfflineSession(indexedFileId, cachedPath);
            }
        }

        // Fallback to server streaming
        var storedDeviceId = _deviceStorageService.Get(PreferenceKeys.DEVICE_ID);

        if (!string.IsNullOrWhiteSpace(storedDeviceId))
        {
            var maxBitrate = _deviceStorageService.Get(PreferenceKeys.STREAMING_QUALITY_WIFI, 0);
            var downmix = _deviceStorageService.Get(PreferenceKeys.DOWNMIX_TO_STEREO, false);

            var request = new CreateStreamSessionRequest
            {
                IndexedFileId = indexedFileId,
                DeviceId = Guid.Parse(storedDeviceId),
                AudioTrackIndex = audioTrackIndex,
                MaxAudioBitrate = maxBitrate > 0 ? maxBitrate : null,
                DownmixToStereo = downmix
            };

            var session = await _streamingService.CreateStreamSessionAsync(request, cancellationToken)
                         ?? throw new Exception($"No streaming session created for IndexedFile with id '{indexedFileId}'.");

            if (session.Source is not null)
            {
                session.Source.Uri = _k7ServerService.GetAbsoluteUri(session.Source.Uri.OriginalString)!;
            }

            return session;
        }

        throw new InvalidOperationException($"Missing {nameof(PreferenceKeys.DEVICE_ID)}");
    }

    public async Task<StreamingSessionDto?> GetOrCreateRemoteSessionAsync(Guid remoteFileId, int? audioTrackIndex = null, CancellationToken cancellationToken = default)
    {
        var storedDeviceId = _deviceStorageService.Get(PreferenceKeys.DEVICE_ID);

        if (string.IsNullOrWhiteSpace(storedDeviceId))
            throw new InvalidOperationException($"Missing {nameof(PreferenceKeys.DEVICE_ID)}");

        var request = new CreateRemoteStreamSessionRequest
        {
            RemoteFileId = remoteFileId,
            DeviceId = Guid.Parse(storedDeviceId),
            AudioTrackIndex = audioTrackIndex
        };

        var session = await _streamingService.CreateRemoteStreamSessionAsync(request, cancellationToken);
        if (session?.Source is not null)
        {
            session.Source.Uri = _k7ServerService.GetAbsoluteUri(session.Source.Uri.OriginalString)!;
        }

        return session;
    }

    private static StreamingSessionDto CreateOfflineSession(Guid indexedFileId, string localPath)
    {
        return new StreamingSessionDto
        {
            Id = Guid.NewGuid(),
            IndexedFileId = indexedFileId,
            PlaybackSettings = new PlaybackSettingsDto(),
            Source = new IndexedFileStreamUri
            {
                Uri = new Uri(localPath),
                MimeType = GetMimeTypeFromExtension(localPath)
            }
        };
    }

    private static string GetMimeTypeFromExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".m4a" => "audio/mp4",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".ogg" or ".opus" => "audio/ogg",
            _ => "application/octet-stream"
        };
    }
}
