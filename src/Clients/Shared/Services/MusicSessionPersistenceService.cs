using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using K7.Shared.Dtos.Requests;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Services;

public sealed class MusicSessionPersistenceService : IMusicSessionPersistence, IDisposable
{
    private static readonly TimeSpan PositionInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StructureDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan PositionDelay = TimeSpan.FromSeconds(2);

    private readonly IAudioPlayerService _audio;
    private readonly IMusicRadioPlaybackService _radio;
    private readonly IMusicSessionStore _store;
    private readonly IMusicSessionApi _api;
    private readonly IDeviceStorageService _deviceStorage;
    private readonly IUserPreferencesService _preferences;
    private readonly IMediaService _media;
    private readonly IPlaylistService _playlists;
    private readonly IK7ServerService _server;
    private readonly ISyncPlayService _syncPlay;
    private readonly ISharedProfileSessionService _sharedProfiles;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<MusicSessionPersistenceService> _logger;

    private string? _scopeKey;
    private string? _boundUser;
    private int _bindVersion;
    private bool _ready;
    private bool _remember = true;
    private string? _fingerprint;
    private DateTimeOffset _lastPositionWrite = DateTimeOffset.MinValue;
    private int _localRevision;
    private CancellationTokenSource? _saveCts;

    public MusicSessionPersistenceService(
        IAudioPlayerService audio,
        IMusicRadioPlaybackService radio,
        IMusicSessionStore store,
        IMusicSessionApi api,
        IDeviceStorageService deviceStorage,
        IUserPreferencesService preferences,
        IMediaService media,
        IPlaylistService playlists,
        IK7ServerService server,
        ISyncPlayService syncPlay,
        ISharedProfileSessionService sharedProfiles,
        IConnectivityService connectivity,
        ILogger<MusicSessionPersistenceService> logger)
    {
        _audio = audio;
        _radio = radio;
        _store = store;
        _api = api;
        _deviceStorage = deviceStorage;
        _preferences = preferences;
        _media = media;
        _playlists = playlists;
        _server = server;
        _syncPlay = syncPlay;
        _sharedProfiles = sharedProfiles;
        _connectivity = connectivity;
        _logger = logger;
        _audio.QueueChanged += OnQueueChanged;
        _audio.PlaybackStateChanged += OnPlaybackStateChanged;
        _audio.CurrentTimeChanged += OnTimeChanged;
        _audio.RepeatModeChanged += OnStructureChanged;
        _audio.ShuffleChanged += OnStructureChanged;
    }

    public async Task BindAndRestoreAsync(string identityUserId, CancellationToken cancellationToken = default)
    {
        var version = ++_bindVersion;
        if (_boundUser is not null && _boundUser != identityUserId)
            await ResetForUserSwitchAsync();

        var profile = _sharedProfiles.ActiveGroupId;
        _scopeKey = profile is Guid profileId
            ? $"{identityUserId}.{profileId:D}"
            : $"{identityUserId}.none";

        _remember = await ReadRememberAsync(cancellationToken);
        if (version != _bindVersion)
            return;

        if (_boundUser == identityUserId && _ready)
        {
            _scopeKey = profile is Guid existingProfile
                ? $"{identityUserId}.{existingProfile:D}"
                : $"{identityUserId}.none";
            return;
        }

        _boundUser = identityUserId;
        if (!_remember || _syncPlay.IsInGroup || _audio.Queue.Count > 0)
        {
            _ready = true;
            return;
        }

        var local = _store.Read(_scopeKey);
        var startup = await ResolveStartupSnapshotAsync(local, cancellationToken);
        if (startup.Snapshot is { Items.Count: > 0 })
            Adopt(startup.Snapshot, replace: false);

        _ready = true;
        if (startup.Upload)
            await FlushAsync();
    }

    /// <summary>
    /// Another identity signs in on this device: the previous user's queue must neither stay on
    /// screen nor be saved under the new scope (the non-empty queue used to short-circuit the
    /// restore, so a user switch kept the other user's session). Its local and server copies
    /// were already written under the previous scope on each change.
    /// </summary>
    private async Task ResetForUserSwitchAsync()
    {
        _ready = false;
        _saveCts?.Cancel();
        _fingerprint = null;
        _droppedFinishedQueue = false;
        _radio.StopRefill();
        _audio.Stop();
        _audio.ClearQueue();
        await _audio.HideAsync();
    }

    public void Adopt(MusicSessionSnapshotDto snapshot, bool replace)
    {
        if (snapshot.Radio is not null)
            _radio.AttachWithoutPlaying(MusicSessionMapper.ToRadioRequest(snapshot.Radio));

        _audio.RestorePaused(snapshot, replace);
        if (Capture() is { } captured)
            _fingerprint = Fingerprint(captured);
        _ = RefillAsync(snapshot);
    }

    public async Task ApplyRememberSettingAsync(bool remember, CancellationToken cancellationToken = default)
    {
        _remember = remember;
        if (remember)
            return;

        await DeleteCurrentAsync(cancellationToken);
    }

    public void DiscardLocal()
    {
        if (_scopeKey is null)
            return;

        _store.Delete(_scopeKey);
        _fingerprint = null;
    }

    public void Flush() => _ = FlushAsync();

    public async Task FlushAsync()
    {
        if (!_ready || !_remember || _scopeKey is null || _syncPlay.IsInGroup)
            return;

        var snapshot = Capture();
        if (snapshot is null)
            return;

        _store.Write(_scopeKey, snapshot);
        MarkUnsynced();
        await PushServerAsync(snapshot, positionOnly: false, CancellationToken.None).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _audio.QueueChanged -= OnQueueChanged;
        _audio.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audio.CurrentTimeChanged -= OnTimeChanged;
        _audio.RepeatModeChanged -= OnStructureChanged;
        _audio.ShuffleChanged -= OnStructureChanged;
        _saveCts?.Cancel();
        _saveCts?.Dispose();
    }

    private bool _droppedFinishedQueue;

    private void OnPlaybackStateChanged(PlaybackState state)
    {
        if (_audio.IsQueueExhausted)
        {
            if (_droppedFinishedQueue || !ReadyToPersist())
                return;

            _droppedFinishedQueue = true;
            _ = DeleteCurrentAsync(CancellationToken.None);
            return;
        }

        if (!_droppedFinishedQueue || state is not (PlaybackState.Playing or PlaybackState.Buffering))
            return;

        _droppedFinishedQueue = false;
        ScheduleStructure();
    }

    private void OnQueueChanged() => ScheduleStructure();

    private void OnStructureChanged(RepeatMode _) => ScheduleStructure();

    private void OnStructureChanged(bool _) => ScheduleStructure();

    private void OnTimeChanged(double _)
    {
        if (!CanPersist())
            return;

        var now = DateTimeOffset.UtcNow;
        if (now - _lastPositionWrite < PositionInterval)
            return;

        _lastPositionWrite = now;
        ScheduleSave(positionOnly: true);
    }

    private void ScheduleStructure()
    {
        if (!CanPersist())
            return;

        if (_audio.Queue.Count == 0)
        {
            _ = DeleteCurrentAsync(CancellationToken.None);
            return;
        }

        ScheduleSave(positionOnly: false);
    }

    private bool CanPersist() =>
        ReadyToPersist() && !_audio.IsQueueExhausted;

    private bool ReadyToPersist() =>
        _ready && _remember && _scopeKey is not null && !_syncPlay.IsInGroup;

    private void ScheduleSave(bool positionOnly)
    {
        WriteLocal();
        _saveCts?.Cancel();
        _saveCts?.Dispose();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;
        _ = SaveAfterDelayAsync(positionOnly, token);
    }

    private void WriteLocal()
    {
        if (_scopeKey is null)
            return;

        var snapshot = Capture();
        if (snapshot is null)
            return;

        _store.Write(_scopeKey, snapshot);
        MarkUnsynced();
    }

    private async Task SaveAfterDelayAsync(bool positionOnly, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(positionOnly ? PositionDelay : StructureDelay, cancellationToken);
            if (_audio.IsQueueExhausted)
                return;
            var snapshot = Capture();
            if (snapshot is null || _scopeKey is null)
                return;

            _store.Write(_scopeKey, snapshot);
            MarkUnsynced();
            var fingerprint = Fingerprint(snapshot);
            var sameShape = fingerprint == _fingerprint;
            _fingerprint = fingerprint;
            await PushServerAsync(snapshot, positionOnly && sameShape, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Music session save failed");
        }
    }

    private async Task PushServerAsync(MusicSessionSnapshotDto snapshot, bool positionOnly, CancellationToken cancellationToken)
    {
        if (!_connectivity.IsOnline || !TryGetDeviceId(out var deviceId))
            return;

        var revision = _localRevision;
        try
        {
            await _api.UpsertMusicSessionAsync(new UpsertMusicSessionRequest
            {
                DeviceId = deviceId,
                PositionOnly = positionOnly,
                Snapshot = snapshot
            }, cancellationToken).ConfigureAwait(false);
            if (_audio.IsQueueExhausted)
            {
                await DeleteCurrentAsync(CancellationToken.None).ConfigureAwait(false);
                return;
            }
            if (revision == _localRevision && _scopeKey is not null)
                _store.SetSynced(_scopeKey, true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Music session upload failed for device {DeviceId}", deviceId);
        }
    }

    private async Task DeleteCurrentAsync(CancellationToken cancellationToken)
    {
        _saveCts?.Cancel();
        _fingerprint = null;
        if (_scopeKey is not null)
            _store.Delete(_scopeKey);

        if (!_connectivity.IsOnline || !TryGetDeviceId(out var deviceId))
            return;

        try
        {
            await _api.DeleteMusicSessionAsync(deviceId, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Music session delete failed for device {DeviceId}", deviceId);
        }
    }

    private MusicSessionSnapshotDto? Capture()
    {
        var queue = _audio.Queue;
        var index = _audio.CurrentIndex;
        if (queue.Count == 0 || index < 0 || index >= queue.Count)
            return null;

        var radio = _audio.ActiveRadioTitle is not null ? _radio.ActiveRequest : null;
        var kind = radio is not null ? MusicSessionSourceKind.Radio : _audio.SessionSource;
        var hasSource = kind != MusicSessionSourceKind.AdHoc;
        var slice = MusicSessionWindow.Slice(queue, index, hasSource);
        var current = queue[index];

        return new MusicSessionSnapshotDto
        {
            SourceKind = kind,
            SourceId = kind == MusicSessionSourceKind.Radio ? null : _audio.SessionSourceId,
            Radio = radio is null ? null : MusicSessionMapper.ToRadioDto(radio),
            CurrentMediaId = current.MediaId,
            CurrentIndexedFileId = current.IndexedFileId,
            CurrentIndex = slice.CurrentIndex,
            PositionSeconds = _audio.CurrentTime,
            RepeatMode = (int)_audio.Repeat,
            Shuffle = _audio.Shuffle,
            ShuffleSeed = _audio.ShuffleSeed,
            Items = slice.Items.Select(MusicSessionMapper.ToSnapshotTrack).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task RefillAsync(MusicSessionSnapshotDto snapshot)
    {
        if (snapshot.CurrentMediaId is not Guid mediaId)
            return;

        try
        {
            var tracks = snapshot.SourceKind switch
            {
                MusicSessionSourceKind.Playlist when snapshot.SourceId is Guid playlistId =>
                    await LoadPlaylistAsync(playlistId),
                MusicSessionSourceKind.Album when snapshot.SourceId is Guid albumId =>
                    await LoadAlbumAsync(albumId),
                MusicSessionSourceKind.Artist when snapshot.SourceId is Guid artistId =>
                    await LoadArtistAsync(artistId),
                _ => null
            };

            if (tracks is not { Count: > 0 } || tracks.All(t => t.MediaId != mediaId))
                return;

            _audio.ReplaceQueueFromSource(tracks, mediaId, snapshot.Shuffle, snapshot.ShuffleSeed);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Music session refill failed for {Source}", snapshot.SourceKind);
        }
    }

    private async Task<List<AudioQueueItem>?> LoadAlbumAsync(Guid albumId)
    {
        var media = await _media.GetMediaAsync(albumId);
        if (media is not MusicAlbumDto album || album.Tracks is not { Count: > 0 } tracks)
            return null;

        return tracks
            .Select(t => MusicTrackQueueMapper.ToQueueItem(t, _server))
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();
    }

    private async Task<List<AudioQueueItem>?> LoadArtistAsync(Guid artistId)
    {
        var items = new List<AudioQueueItem>();
        for (var page = 1; page <= 5 && items.Count < 1000; page++)
        {
            var result = await _media.GetLiteMediasAsync(new GetMediasWithPaginationQuery
            {
                MediaTypes = [MediaType.MusicTrack],
                ArtistIds = [artistId],
                OrderBy = [MediaOrderingOption.TitleAsc],
                PageNumber = page,
                PageSize = 200
            });

            var batch = (result?.Items ?? [])
                .OfType<LiteMusicTrackDto>()
                .Select(t => MusicTrackQueueMapper.ToQueueItem(t, _server))
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList();

            if (batch.Count == 0)
                break;

            items.AddRange(batch);
            if (result?.HasNextPage != true)
                break;
        }

        return items;
    }

    private async Task<List<AudioQueueItem>?> LoadPlaylistAsync(Guid playlistId)
    {
        var items = new List<AudioQueueItem>();
        var page = 1;

        while (true)
        {
            var result = await _playlists.GetPlaylistItemsAsync(playlistId, page, 100);
            var batch = result?.Items ?? [];
            if (batch.Count == 0)
                break;

            foreach (var item in batch)
            {
                if (ToPlaylistQueueItem(item) is { } track)
                    items.Add(track);
            }

            if (result?.HasNextPage != true)
                break;

            page++;
        }

        return items;
    }

    private AudioQueueItem? ToPlaylistQueueItem(PlaylistItemDto item)
    {
        if (item.IndexedFileId is not Guid fileId)
            return null;

        var cover = item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);

        return new AudioQueueItem
        {
            IndexedFileId = fileId,
            MediaId = item.MediaId,
            Title = item.MediaTitle ?? string.Empty,
            Artist = item.ArtistName,
            ArtistId = item.ArtistId,
            AlbumTitle = item.AlbumTitle,
            Genre = item.Genre,
            Duration = item.Duration,
            UserRating = item.UserRating,
            CoverUrl = _server.GetAbsoluteUri(cover?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
        };
    }

    private async Task<(MusicSessionSnapshotDto? Snapshot, bool Upload)> ResolveStartupSnapshotAsync(
        MusicSessionSnapshotDto? local,
        CancellationToken cancellationToken)
    {
        if (!_connectivity.IsOnline || !TryGetDeviceId(out var deviceId))
            return (local, false);

        try
        {
            var remote = await _api.GetMusicSessionAsync(deviceId, cancellationToken);
            if (remote is { Items.Count: > 0 })
                return (remote, false);

            if (local is null || _scopeKey is null)
                return (null, false);

            if (_store.IsSynced(_scopeKey))
            {
                _store.Delete(_scopeKey);
                return (null, false);
            }

            return (local, true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Music session fetch failed for device {DeviceId}", deviceId);
            return (local, false);
        }
    }

    private void MarkUnsynced()
    {
        if (_scopeKey is null)
            return;

        _store.SetSynced(_scopeKey, false);
        _localRevision++;
    }

    private async Task<bool> ReadRememberAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _preferences.GetEffectiveAudioPlayerSettingsAsync(cancellationToken);
            return settings.RememberMusicSession != false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return true;
        }
    }

    private bool TryGetDeviceId(out Guid deviceId)
    {
        var raw = _deviceStorage.Get(PreferenceKeys.DEVICE_ID);
        return Guid.TryParse(raw, out deviceId) && deviceId != Guid.Empty;
    }

    private static string Fingerprint(MusicSessionSnapshotDto snapshot) =>
        string.Join(
            '|',
            snapshot.SourceKind,
            snapshot.SourceId,
            snapshot.Shuffle,
            snapshot.ShuffleSeed,
            snapshot.RepeatMode,
            snapshot.CurrentMediaId,
            string.Join(',', snapshot.Items.Select(i => i.MediaId)));
}
