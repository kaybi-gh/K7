using System.Security.Claims;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Diagnostics;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.Search;
using K7.Shared.Dtos.Users;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.DesignSystem.Mocks;

public sealed class MockAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState _state = new(
        new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "Design User"),
                new Claim(ClaimTypes.NameIdentifier, "design-user-id"),
                new Claim(ClaimTypes.Role, "Admin"),
            ],
            authenticationType: "mock")));

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_state);
}

public sealed class MockCustomAuthStateProvider : ICustomAuthenticationStateProvider
{
    public Task LoginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoginAsGuestAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> SwitchToUserAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void SignInOffline(LocalUser user) { }
    public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockFeatureAccessService : IFeatureAccessService
{
    public Task<bool> HasCapabilityAsync(Capability capability) => Task.FromResult(true);
    public Task<string?> GetRoleAsync() => Task.FromResult<string?>("admin");
}

public sealed class MockDeviceService : IDeviceService
{
    public string? GetDeviceId() => "design-device";
    public string? GetDeviceUniqueId() => "design-unique-id";
    public ClientType GetClientType() => ClientType.Web;
    public Task<DeviceType> GetDeviceTypeAsync() => Task.FromResult(DeviceType.Desktop);
    public Task<K7.Server.Domain.Enums.OperatingSystem> GetOperatingSystemAsync() => Task.FromResult(K7.Server.Domain.Enums.OperatingSystem.Windows);
    public Task<NativeDeviceDetailsDto> GetNativeDeviceDetailsAsync() => Task.FromResult(new NativeDeviceDetailsDto());
    public Task<WebDeviceDetailsDto> GetWebDeviceDetailsAsync() => Task.FromResult(new WebDeviceDetailsDto());
    public Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync() => Task.FromResult(new List<MediaFormatDto>());
    public Task<DeviceCodecSummaryDto> GetDeviceCodecSummaryAsync() => Task.FromResult(new DeviceCodecSummaryDto
    {
        Containers = ["mp4", "webm"],
        AudioCodecs = ["aac", "opus"],
        VideoCodecs = ["h264", "vp9"]
    });
    public Task<bool> GetHdrSupportAsync() => Task.FromResult(false);
    public Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync() => Task.FromResult(new CreateDeviceRequest { PlaybackCapabilities = new CreateDeviceRequestPlaybackCapibilities() });
    public string? GetLocalFileUrl(string? localPath) => null;
}

public sealed class MockDeviceStorageService : IDeviceStorageService
{
    public T? Get<T>(PreferenceKey<T> key, T? defaultValue = default) => defaultValue;
    public void Set<T>(PreferenceKey<T> key, T value) { }
    public void Remove<T>(PreferenceKey<T> key) { }
}

public sealed class MockPageFilterStorage : IPageFilterStorage
{
    public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
        Task.FromResult<T?>(null);

    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task ClearAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class MockVolumeService : IVolumeService
{
    public bool SupportsNativeVolume => false;
    public double Volume => 1.0;
    public void SetVolume(double volume) { }
}

public sealed class MockBrightnessService : IBrightnessService
{
    public bool SupportsNativeBrightness => false;
    public double Brightness => 1.0;
    public void SetBrightness(double brightness) { }
    public void ResetBrightness() { }
}

public sealed class MockStreamUriService : IStreamUriService
{
    public Task<StreamingSessionDto> GetOrCreateSessionAsync(Guid indexedFileId, int? audioTrackIndex = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new StreamingSessionDto { Id = Guid.NewGuid(), IndexedFileId = indexedFileId, PlaybackSettings = new PlaybackSettingsDto() });

    public Task<StreamingSessionDto?> GetOrCreateRemoteSessionAsync(Guid remoteFileId, int? audioTrackIndex = null, CancellationToken cancellationToken = default)
        => Task.FromResult<StreamingSessionDto?>(new StreamingSessionDto { Id = Guid.NewGuid(), IndexedFileId = remoteFileId, PlaybackSettings = new PlaybackSettingsDto() });
}

public sealed class MockLocalUserService : ILocalUserService
{
    public bool IsSingleUserMode { get; set; }
    public List<LocalUser> GetAll() => [];
    public LocalUser? GetLastActive() => null;
    public void SaveOrUpdate(LocalUser user) { }
    public void Remove(string identityUserId) { }
    public void SetLastActiveId(string identityUserId) { }
    public void SetPin(string identityUserId, string? pin) { }
    public bool VerifyPin(string identityUserId, string pin) => false;
}

public sealed class MockMediaPlayerService : IMediaPlayerService
{
#pragma warning disable CS0067
    public ActivePlayerType ActivePlayer => ActivePlayerType.None;
    public event Action<ActivePlayerType>? ActivePlayerChanged;
#pragma warning restore CS0067
    public void Dispose() { }
}

// --- IAudioPlayerService ---

#pragma warning disable CS0067
public sealed class MockAudioPlayerService : IAudioPlayerService, IDisposable
{
    public event Func<Task>? PlayRequested;
    public event Func<Task>? PauseRequested;
    public event Func<Task>? StopRequested;
    public event Func<double, Task>? SeekRequested;
    public event Func<Task>? MuteRequested;
    public event Func<Task>? UnmuteRequested;
    public event Func<double, Task>? VolumeChangeRequested;
    public event Action<PlayerSource>? SourceChanged;
    public event Action? IsVisibleChanged;
    public event Action<PlaybackState>? PlaybackStateChanged;
    public event Action<double>? DurationChanged;
    public event Action<double>? CurrentTimeChanged;
    public event Action<double>? BufferedTimeChanged;
    public event Action<double>? VolumeChanged;
    public event Action<bool>? IsMutedChanged;
    public event Action? QueueChanged;
    public event Action<AudioQueueItem?>? CurrentTrackChanged;
    public event Action<RepeatMode>? RepeatModeChanged;
    public event Action<bool>? ShuffleChanged;
    public event Action? ActiveRadioChanged;
    public event Action? ActivePlaylistChanged;
    public event Func<PlayerSource, double, Task>? CrossfadeRequested;
    public event Action? CrossfadeDurationChanged;
    public event Action? IsFullScreenVisibleChanged;

    public bool IsMuted { get; set; }
    private bool _audioVisible;
    private AudioQueueItem? _currentTrack;
    private List<AudioQueueItem> _queue = [];
    private int _currentIndex;
    private Timer? _progressTimer;

    public bool IsVisible => _audioVisible;
    public IReadOnlyList<AudioQueueItem> Queue => _queue;
    public AudioQueueItem? CurrentTrack => _currentTrack;
    public int CurrentIndex => _currentIndex;
    public RepeatMode Repeat => RepeatMode.Off;
    public bool Shuffle => false;
    public string? ActiveRadioTitle { get; private set; }
    public Guid? ActivePlaylistId { get; private set; }
    public bool AdaptiveCrossfade => false;
    public double CrossfadeDuration => 0;
    public double CrossfadeTriggerWindow => 0;
    private bool _isFullScreenVisible;
    public bool IsFullScreenVisible => _isFullScreenVisible;
    public PlaybackState PlaybackState { get; set; } = PlaybackState.Idle;
    public double Duration { get; set; }
    public double CurrentTime { get; set; }
    public double BufferedTime { get; set; }
    public double Volume { get; set; } = 1.0;

    public void Play()
    {
        if (_currentTrack is null) return;
        PlaybackState = PlaybackState.Playing;
        PlaybackStateChanged?.Invoke(PlaybackState);
        _progressTimer ??= new Timer(_ =>
        {
            CurrentTime = Math.Min(CurrentTime + 1, Duration);
            CurrentTimeChanged?.Invoke(CurrentTime);
            if (CurrentTime >= Duration && Duration > 0)
                _ = OnTrackEndedAsync();
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Pause()
    {
        _progressTimer?.Dispose();
        _progressTimer = null;
        PlaybackState = PlaybackState.Paused;
        PlaybackStateChanged?.Invoke(PlaybackState);
    }

    public void Stop()
    {
        _progressTimer?.Dispose();
        _progressTimer = null;
        PlaybackState = PlaybackState.Idle;
        CurrentTime = 0;
        PlaybackStateChanged?.Invoke(PlaybackState);
        CurrentTimeChanged?.Invoke(0);
    }

    public void Seek(double time)
    {
        CurrentTime = time;
        CurrentTimeChanged?.Invoke(CurrentTime);
        _ = SeekRequested?.Invoke(time);
    }
    public void Mute() { IsMuted = true; IsMutedChanged?.Invoke(true); }
    public void Unmute() { IsMuted = false; IsMutedChanged?.Invoke(false); }
    public void SetVolume(double volume) { Volume = volume; VolumeChanged?.Invoke(volume); }

    public Task PlayTrackAsync(AudioQueueItem track, CancellationToken cancellationToken = default)
    {
        _queue = [track];
        _currentIndex = 0;
        SetCurrentTrack(track);
        Play();
        return Task.CompletedTask;
    }

    public Task PlayTracksAsync(IEnumerable<AudioQueueItem> tracks, int startIndex = 0, Guid? playlistId = null, CancellationToken cancellationToken = default)
    {
        ActiveRadioTitle = null;
        ActiveRadioChanged?.Invoke();
        _queue = [.. tracks];
        _currentIndex = startIndex;
        if (_queue.Count > 0) { SetCurrentTrack(_queue[_currentIndex]); Play(); }
        return Task.CompletedTask;
    }

    public Task PlayRadioAsync(IEnumerable<AudioQueueItem> tracks, string radioTitle, int startIndex = 0, CancellationToken cancellationToken = default)
    {
        ActiveRadioTitle = radioTitle;
        ActiveRadioChanged?.Invoke();
        _queue = [.. tracks];
        _currentIndex = startIndex;
        if (_queue.Count > 0) { SetCurrentTrack(_queue[_currentIndex]); Play(); }
        return Task.CompletedTask;
    }

    public void AddToQueue(AudioQueueItem track) { _queue.Add(track); QueueChanged?.Invoke(); }
    public void AddToQueueNext(AudioQueueItem track) { _queue.Insert(_currentIndex + 1, track); QueueChanged?.Invoke(); }
    public void RemoveFromQueue(int index) { if (index >= 0 && index < _queue.Count) { _queue.RemoveAt(index); QueueChanged?.Invoke(); } }
    public void ClearQueue() { _queue.Clear(); QueueChanged?.Invoke(); }

    public Task SkipToIndexAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= _queue.Count || index == _currentIndex) return Task.CompletedTask;
        _currentIndex = index;
        SetCurrentTrack(_queue[_currentIndex]);
        Play();
        return Task.CompletedTask;
    }

    public Task NextAsync(CancellationToken cancellationToken = default)
    {
        if (_queue.Count == 0) return Task.CompletedTask;
        _currentIndex = (_currentIndex + 1) % _queue.Count;
        SetCurrentTrack(_queue[_currentIndex]);
        Play();
        return Task.CompletedTask;
    }

    public Task PreviousAsync(CancellationToken cancellationToken = default)
    {
        if (_queue.Count == 0) return Task.CompletedTask;
        _currentIndex = (_currentIndex - 1 + _queue.Count) % _queue.Count;
        SetCurrentTrack(_queue[_currentIndex]);
        Play();
        return Task.CompletedTask;
    }

    public void ToggleShuffle() { }
    public void CycleRepeatMode() { }
    public void ToggleAdaptiveCrossfade() { }
    public void SetCrossfadeDuration(double seconds) { }
    public Task OnCrossfadeNeededAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    // Loudness normalization
    public event Action? LoudnessSettingsChanged;
    public bool LoudnessEnabled => true;
    public double LoudnessTargetLufs => -14.0;
    public double LoudnessPreampDb => 0.0;
    public bool LimiterEnabled => true;
    public void SetLoudnessEnabled(bool enabled) => LoudnessSettingsChanged?.Invoke();
    public void SetLoudnessTargetLufs(double lufs) => LoudnessSettingsChanged?.Invoke();
    public void SetLoudnessPreampDb(double db) => LoudnessSettingsChanged?.Invoke();
    public void SetLimiterEnabled(bool enabled) => LoudnessSettingsChanged?.Invoke();

    // EQ
    public event Action? EqSettingsChanged;
    public bool EqEnabled => false;
    public double[] EqBands => new double[10];
    public string? EqPresetName => null;
    public void SetEqEnabled(bool enabled) => EqSettingsChanged?.Invoke();
    public void SetEqBands(double[] bands) => EqSettingsChanged?.Invoke();
    public void SetEqPresetName(string? name) => EqSettingsChanged?.Invoke();

    // Player UX settings
    public event Action? PlayerUxSettingsChanged;
    public bool ShowFullscreenOnPlay => false;
    public int SkipBackSeconds => 5;
    public int SkipForwardSeconds => 15;
    public bool KeepScreenOn => false;
    public void SetShowFullscreenOnPlay(bool value) => PlayerUxSettingsChanged?.Invoke();
    public void SetSkipBackSeconds(int value) => PlayerUxSettingsChanged?.Invoke();
    public void SetSkipForwardSeconds(int value) => PlayerUxSettingsChanged?.Invoke();
    public void SetKeepScreenOn(bool value) => PlayerUxSettingsChanged?.Invoke();

    // Gapless
    public event Func<PlayerSource, Task>? GaplessPrebufferRequested;
    public Task OnGaplessPrebufferNeededAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void ToggleFullScreen() { _isFullScreenVisible = !_isFullScreenVisible; IsFullScreenVisibleChanged?.Invoke(); }
    public Task ShowAsync() { _audioVisible = true; IsVisibleChanged?.Invoke(); return Task.CompletedTask; }
    public Task HideAsync() { _audioVisible = false; IsVisibleChanged?.Invoke(); return Task.CompletedTask; }

    public Task OnTrackEndedAsync(CancellationToken cancellationToken = default)
    {
        if (_currentIndex + 1 < _queue.Count) return NextAsync(cancellationToken);
        Stop();
        return Task.CompletedTask;
    }

    private void SetCurrentTrack(AudioQueueItem track)
    {
        _progressTimer?.Dispose();
        _progressTimer = null;
        _currentTrack = track;
        Duration = track.Duration ?? 180;
        CurrentTime = 0;
        DurationChanged?.Invoke(Duration);
        CurrentTimeChanged?.Invoke(0);
        CurrentTrackChanged?.Invoke(_currentTrack);
        QueueChanged?.Invoke();
        _audioVisible = true;
        IsVisibleChanged?.Invoke();
    }

    // Legacy helper kept for compat
    public void SetDemoTrack(AudioQueueItem track) => SetCurrentTrack(track);

    public void Dispose() { _progressTimer?.Dispose(); _progressTimer = null; }
}

// --- Server API mocks ---

public sealed class MockK7ServerService : IK7ServerService
{
    public HttpClient HttpClient { get; } = new();
    public Uri? GetAbsoluteUri(string? relativePath) => null;
}

public sealed class MockMediaService : IMediaService
{
    public Task<PaginatedListDto<HomeFeedItemDto>?> GetHomeFeedAsync(GetHomeFeedQuery query, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<HomeFeedItemDto>?>(null);
    public Task<List<MediaFormatDto>> GetMediaFormatsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<MediaFormatDto>());
    public Task<MovieDto?> GetMovieAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<MovieDto?>(null);
    public Task<MediaDto?> GetMediaAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<MediaDto?>(null);
    public Task<PaginatedListDto<LiteMediaDto>?> GetLiteMediasAsync(GetMediasWithPaginationQuery query, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<LiteMediaDto>?>(null);
    public Task<PaginatedListDto<LiteMediaDto>?> QueryMediasAsync(QueryMediasRequest request, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<LiteMediaDto>?>(null);
    public Task<MediaTagsDto?> GetMediaTagsAsync(GetMediaTagsQuery query, CancellationToken cancellationToken = default) => Task.FromResult<MediaTagsDto?>(null);

    public Task<IReadOnlyList<string>?> GetMediaBrowseFilterSuggestionsAsync(GetMediaBrowseFilterSuggestionsQuery query, CancellationToken cancellationToken = default)
    {
        string[] samples = query.Field switch
        {
            nameof(SmartPlaylistField.ActorName) => ["Leonardo DiCaprio", "Tom Hanks", "Bryan Cranston"],
            nameof(SmartPlaylistField.ArtistName) => ["Daft Punk", "Radiohead", "Bjork"],
            "Studio" => ["Warner Bros.", "Universal Pictures", "Paramount Pictures"],
            "Network" => ["HBO", "Netflix", "AMC"],
            _ => []
        };

        if (string.IsNullOrWhiteSpace(query.SearchText))
            return Task.FromResult<IReadOnlyList<string>?>([]);

        var term = query.SearchText.Trim();
        var matches = samples
            .Where(s => s.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(query.Limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>?>(matches);
    }
    public Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PersonDto?>(null);
    public Task<PaginatedListDto<PersonDto>?> GetPersonsAsync(GetPersonsWithPaginationQuery query, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<PersonDto>?>(null);
    public Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year = null, string? providerId = null, MediaType? mediaType = null, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MetadataSearchResult>());
    public Task ReidentifyIndexedFileAsync(Guid id, ReidentifyIndexedFileRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReidentifyMediaAsync(Guid id, ReidentifyMediaRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RefreshMediaMetadataAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateMediaMetadataAsync(Guid id, UpdateMediaMetadataRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Guid> UploadMediaPictureAsync(Guid mediaId, Stream stream, string fileName, MetadataPictureType pictureType, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task DeleteMediaPictureAsync(Guid mediaId, Guid pictureId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<ProviderImageDto>> GetMediaProviderImagesAsync(Guid mediaId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProviderImageDto>>([]);
    public Task<Guid> ImportMediaPictureFromUrlAsync(Guid mediaId, ImportMediaPictureFromUrlRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task RefreshPersonMetadataAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdatePersonMetadataAsync(Guid id, UpdatePersonMetadataRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Guid> UploadPersonPictureAsync(Guid personId, Stream stream, string fileName, MetadataPictureType pictureType, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task DeletePersonPictureAsync(Guid personId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Guid> ImportPersonPictureFromUrlAsync(Guid personId, ImportMediaPictureFromUrlRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task<IReadOnlyList<ProviderImageDto>> GetPersonProviderImagesAsync(Guid personId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProviderImageDto>>([]);
    public Task<LiteSerieEpisodeDto?> GetNextEpisodeAsync(Guid serieId, Guid currentEpisodeId, CancellationToken cancellationToken = default) => Task.FromResult<LiteSerieEpisodeDto?>(null);
    public Task<IReadOnlyList<K7.Shared.Dtos.Entities.Medias.MediaSegmentDto>> GetMediaSegmentsAsync(Guid mediaId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<K7.Shared.Dtos.Entities.Medias.MediaSegmentDto>>([]);
    public Task DetectMediaSegmentsAsync(Guid seasonId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<List<LiteMediaDto>> GetSimilarMediaAsync(Guid mediaId, CancellationToken cancellationToken = default) => Task.FromResult(new List<LiteMediaDto>());
    public Task<IReadOnlyList<LiteMusicTrackDto>> GetArtistTopTracksAsync(Guid artistId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LiteMusicTrackDto>>([]);
    public Task<IReadOnlyList<LiteMusicArtistDto>> GetSimilarMusicArtistsAsync(Guid artistId, int count = 12, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LiteMusicArtistDto>>([]);
    public Task<IReadOnlyList<PlayedMusicTrackDto>> GetTopMusicTracksAsync(Guid[]? libraryIds = null, int count = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PlayedMusicTrackDto>>([]);
    public Task<List<K7.Shared.Dtos.Entities.Persons.PersonKnownForItemDto>> GetPersonKnownForAsync(Guid personId, CancellationToken cancellationToken = default) => Task.FromResult(new List<K7.Shared.Dtos.Entities.Persons.PersonKnownForItemDto>());
    public Task<SetMediaWatchStateResultDto?> SetMediaWatchStateAsync(Guid mediaId, bool watched, WatchStateScope scope = WatchStateScope.Item, CancellationToken cancellationToken = default) =>
        Task.FromResult<SetMediaWatchStateResultDto?>(new SetMediaWatchStateResultDto { AffectedMediaIds = [mediaId] });
}

public sealed class MockLibraryService : ILibraryService
{
    public Task<List<LibraryDto>> GetLibrariesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<LibraryDto>());
    public Task<List<LibraryGroupDto>> GetLibraryGroupsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<LibraryGroupDto>());
    public Task<List<LibraryStatisticsDto>> GetLibraryStatisticsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<LibraryStatisticsDto>());
    public Task<Guid> CreateLibraryAsync(CreateLibraryRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task UpdateLibraryAsync(Guid id, UpdateLibraryRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteLibraryAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task IndexLibraryFilesAsync(Guid libraryId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<DirectoryContentDto?> GetDirectoriesAsync(string? path = null, CancellationToken cancellationToken = default) => Task.FromResult<DirectoryContentDto?>(null);
    public Task<List<MetadataProviderInfoDto>> GetMetadataProvidersAsync(LibraryMediaType? mediaType = null, CancellationToken cancellationToken = default) => Task.FromResult(new List<MetadataProviderInfoDto>());
    public Task<Guid> UploadLibraryGroupCoverAsync(Guid libraryGroupId, Stream stream, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task<Guid> SetLibraryGroupCoverFromPictureAsync(Guid libraryGroupId, Guid sourcePictureId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task<List<LibraryPictureDto>> GetLibraryPicturesAsync(Guid libraryId, CancellationToken cancellationToken = default) => Task.FromResult(new List<LibraryPictureDto>());
    public Task UpdateLibraryGroupAsync(Guid id, UpdateLibraryGroupRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteLibraryGroupAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockPlaylistService : IPlaylistService
{
    public Task<PaginatedListDto<LitePlaylistDto>?> GetPlaylistsAsync(int pageNumber = 1, int pageSize = 20, MediaType? mediaType = null, LibraryItemOrderingOption? orderBy = null, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<LitePlaylistDto>?>(null);
    public Task<PlaylistDto?> GetPlaylistAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PlaylistDto?>(null);
    public Task<PaginatedListDto<PlaylistItemDto>?> GetPlaylistItemsAsync(Guid playlistId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<PlaylistItemDto>?>(null);
    public Task<Guid> CreatePlaylistAsync(CreatePlaylistRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task UpdatePlaylistAsync(Guid id, UpdatePlaylistRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeletePlaylistAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Guid> UploadPlaylistCoverAsync(Guid playlistId, Stream stream, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task<Guid> SetPlaylistCoverFromPictureAsync(Guid playlistId, Guid sourcePictureId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task RemovePlaylistCoverAsync(Guid playlistId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Guid> AddPlaylistItemAsync(Guid playlistId, Guid mediaId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task RemovePlaylistItemAsync(Guid playlistId, Guid itemId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RecordPlaylistPlaybackAsync(Guid playlistId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PaginatedListDto<LiteSmartPlaylistDto>?> GetSmartPlaylistsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<LiteSmartPlaylistDto>?>(null);
    public Task<SmartPlaylistDto?> GetSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SmartPlaylistDto?>(null);
    public Task<Guid> CreateSmartPlaylistAsync(CreateSmartPlaylistRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task UpdateSmartPlaylistAsync(Guid id, UpdateSmartPlaylistRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EvaluateSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockStreamingService : IStreamingService
{
    public Task<IndexedFileStreamUri?> GetIndexedFileStreamUriAsync(GetIndexedFileStreamsUriQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IndexedFileStreamUri?>(null);
    public Task<StreamingSessionDto?> CreateStreamSessionAsync(CreateStreamSessionRequest request, CancellationToken cancellationToken = default) => Task.FromResult<StreamingSessionDto?>(null);
    public Task<StreamingSessionDto?> CreateRemoteStreamSessionAsync(CreateRemoteStreamSessionRequest request, CancellationToken cancellationToken = default) => Task.FromResult<StreamingSessionDto?>(null);
    public Task ReportPlaybackProgressAsync(Guid mediaId, Guid sessionId, Guid referenceId, double position, double duration, int state, Guid? deviceId = null, Guid? playlistId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<string?> GenerateEphemeralTokenAsync(Guid streamSessionId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task RevokeEphemeralTokenAsync(Guid streamSessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockDeviceApiService : IDeviceApiService
{
    public Task<Guid> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task AttachCurrentUserToDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PaginatedListDto<DeviceDto>?> GetDevicesAsync(GetDevicesQuery? query = null, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<DeviceDto>?>(null);
    public Task DeleteDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockUserAdminService : IUserAdminService
{
    public Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<UserDto>());
    public Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult<UserDto?>(null);
    public Task UpdateUserRoleAsync(Guid userId, UpdateUserRoleRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateUserCapabilitiesAsync(Guid userId, UpdateUserCapabilitiesRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ToggleUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateUserLibraryExclusionsAsync(Guid userId, UpdateUserLibraryExclusionsRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateUserMediaExclusionsAsync(Guid userId, UpdateUserMediaExclusionsRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> ToggleMediaExclusionAsync(Guid mediaId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<List<LiteMediaDto>> GetSelfMediaExclusionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<LiteMediaDto>());
    public Task UpdateUserPinAsync(Guid userId, string? pin, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<List<ContentRestrictionProfileDto>> GetContentRestrictionProfilesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<ContentRestrictionProfileDto>());
    public Task<Guid> CreateContentRestrictionProfileAsync(CreateContentRestrictionProfileRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task UpdateContentRestrictionProfileAsync(Guid id, UpdateContentRestrictionProfileRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteContentRestrictionProfileAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AssignContentRestrictionProfileAsync(Guid userId, Guid? profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<List<RestrictedMediaPreviewDto>> PreviewRestrictedMediasAsync(Guid profileId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RestrictedMediaPreviewDto>());
    public Task<string?> GetUserLanguageAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task UpdateUserLanguageAsync(string language, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new UserDto { Id = Guid.Empty, UserName = null, Role = "Admin", Created = DateTimeOffset.UtcNow, IsActive = true, IsGuest = false, HasPin = false, CapabilityOverrides = [], LibraryExclusions = [], MediaExclusions = [] });
    public Task MergeUsersAsync(Guid sourceUserId, Guid targetUserId, MergeStrategy? strategy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResetUserPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UploadAvatarAsync(Stream stream, string fileName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAvatarAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemovePasswordAsync(RemovePasswordRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateEmailAsync(UpdateEmailRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteAccountAsync(DeleteAccountRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RestoreUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<LoginMethodsDto> GetLoginMethodsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new LoginMethodsDto { HasPassword = true, CanRemovePassword = false, TwoFactorEnabled = false, RecoveryCodesLeft = 0, ExternalLogins = [] });
    public Task UnlinkExternalLoginAsync(string provider, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<TwoFactorStatusDto> GetTwoFactorStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(new TwoFactorStatusDto { IsEnabled = false, HasAuthenticator = false, RecoveryCodesLeft = 0 });
    public Task<TwoFactorSetupDto> BeginTwoFactorSetupAsync(CancellationToken cancellationToken = default) => Task.FromResult(new TwoFactorSetupDto { SharedKey = "abcd efgh", AuthenticatorUri = "otpauth://totp/K7:user?secret=abcdefgh&issuer=K7&digits=6" });
    public Task<RecoveryCodesDto> VerifyTwoFactorSetupAsync(VerifyTwoFactorRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new RecoveryCodesDto { RecoveryCodes = ["code-1", "code-2"] });
    public Task<RecoveryCodesDto> GenerateTwoFactorRecoveryCodesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new RecoveryCodesDto { RecoveryCodes = ["code-1", "code-2"] });
    public Task DisableTwoFactorAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockRatingService : IRatingService
{
    public Task RateMediaAsync(Guid mediaId, int value, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockBackgroundTaskService : IBackgroundTaskService
{
    public Task<PaginatedListDto<BackgroundTaskDto>> GetBackgroundTasksAsync(int pageNumber = 1, int pageSize = 20, IReadOnlyCollection<BackgroundTaskStatus>? statuses = null, IReadOnlyCollection<string>? names = null, string? sortBy = null, bool sortDescending = true, CancellationToken cancellationToken = default) => Task.FromResult(new PaginatedListDto<BackgroundTaskDto>());
    public Task<BackgroundTaskDto> GetBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(new BackgroundTaskDto { Id = Guid.Empty, Name = "" });
    public Task DeleteBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CancelBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<BackgroundTaskSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new BackgroundTaskSettingsDto { WorkerCount = 1, ConcurrencyGroups = [] });
    public Task UpdateSettingsAsync(UpdateBackgroundTaskSettingsRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<BackgroundTaskSummaryDto> GetSummaryAsync(
        IReadOnlyCollection<BackgroundTaskStatus>? statusFilter = null,
        IReadOnlyCollection<string>? namesFilter = null,
        CancellationToken cancellationToken = default) => Task.FromResult(new BackgroundTaskSummaryDto { TotalCount = 0, StatusCounts = [], TaskTypeCounts = [] });
}

public sealed class MockDiagnosticsService : IDiagnosticsService
{
    public Task<List<LibraryHealthSummaryDto>> GetDiagnosticsSummaryAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<LibraryHealthSummaryDto>());
    public Task<PaginatedListDto<DiagnosticItemDto>> GetDiagnosticItemsAsync(Guid? libraryId = null, DiagnosticEntityType? entityType = null, DiagnosticIssue? issue = null, IReadOnlyCollection<DiagnosticIssue>? issues = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Task.FromResult(new PaginatedListDto<DiagnosticItemDto>());
    public Task<int> FixDiagnosticItemsAsync(IReadOnlyList<Guid> entityIds, DiagnosticFixAction action, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<int> QueueDiagnosticFixesAsync(DiagnosticIssue issue, Guid? libraryId = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
}

public sealed class MockServerInfoService : IServerInfoService
{
    public Task<AboutInfoDto?> GetAboutInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult<AboutInfoDto?>(new AboutInfoDto { ServerVersion = "1.0.0" });
    public Task<ServerInfoDto?> GetServerInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult<ServerInfoDto?>(null);
    public Task<AuthenticationInfoDto?> GetAuthenticationInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult<AuthenticationInfoDto?>(null);
    public Task<WatchStatsDto?> GetWatchStatsAsync(string? mediaType = null, string period = "month", DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) => Task.FromResult<WatchStatsDto?>(null);
    public Task<PlaybackHistoryPageDto?> GetPlaybackHistoryAsync(int page = 1, int pageSize = 25, string? mediaType = null, string period = "month", DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) => Task.FromResult<PlaybackHistoryPageDto?>(null);
    public Task<List<LiteMediaDto>?> GetMusicRadioAsync(string radioType, Guid[]? libraryIds = null, Guid[]? libraryGroupIds = null, Guid? seedTrackId = null, Guid? seedArtistId = null, string? moodPreset = null, int? moodCentroidIndex = null, int limit = 50, Guid[]? excludeIds = null, CancellationToken cancellationToken = default) => Task.FromResult<List<LiteMediaDto>?>(null);
    public Task UpdateDefaultLanguageAsync(string language, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateDefaultThemeAsync(string theme, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<List<ActiveStreamDto>?> GetActiveStreamsAsync(CancellationToken cancellationToken = default) => Task.FromResult<List<ActiveStreamDto>?>(null);
    public Task<ServerMetricsHistoryDto?> GetServerMetricsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var snapshots = Enumerable.Range(0, 12)
            .Select(i => new ServerMetricsSnapshotDto
            {
                Timestamp = now.AddSeconds(-55 + i * 5),
                CpuPercent = 8 + i * 1.5,
                MemoryUsedMb = 420 + i * 4,
                MemoryTotalMb = 8192,
                NetworkMbps = i * 0.15,
                GcHeapMb = 180 + i,
                ThreadCount = 42 + i % 3,
                UptimeSeconds = 3600 + i * 5,
                OnlineUsersCount = 2 + i % 3,
                ConnectedDevicesCount = 3 + i % 4,
                DiskVolumes =
                [
                    new ServerDiskVolumeDto
                    {
                        Label = "Data (C:)",
                        UsedGb = 420 + i,
                        TotalGb = 1024,
                        FreePercent = 58.9 - i * 0.2
                    },
                    new ServerDiskVolumeDto
                    {
                        Label = "Media (D:)",
                        UsedGb = 1800,
                        TotalGb = 2000,
                        FreePercent = 10
                    }
                ]
            })
            .ToList();

        return Task.FromResult<ServerMetricsHistoryDto?>(new ServerMetricsHistoryDto { Snapshots = snapshots });
    }
    public Task<PlaybackHistoryPageDto?> GetAdminPlaybackHistoryAsync(int page = 1, int pageSize = 25, string? mediaType = null, Guid? userId = null, CancellationToken cancellationToken = default) => Task.FromResult<PlaybackHistoryPageDto?>(null);
    public Task<WatchStatsDto?> GetAdminWatchStatsAsync(string? mediaType = null, string period = "month", Guid? userId = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) => Task.FromResult<WatchStatsDto?>(null);
}

public sealed class MockUserPreferencesService : IUserPreferencesService
{
    public Task<IReadOnlyList<Guid>> GetSelfLibraryExclusionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    public Task UpdateSelfLibraryExclusionsAsync(K7.Shared.Dtos.Requests.UpdateSelfLibraryExclusionsRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<K7.Shared.Dtos.Home.HomeLayoutDto> GetHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.Home.HomeLayoutDto { Rows = [] });
    public Task UpdateHomeLayoutAsync(K7.Shared.Dtos.Home.HomeLayoutDto layout, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResetHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<K7.Shared.Dtos.VideoPlayerSettingsDto> GetEffectiveVideoPlayerSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.VideoPlayerSettingsDto());
    public Task UpdateUserVideoPlayerSettingsAsync(K7.Shared.Dtos.VideoPlayerSettingsDto settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResetUserVideoPlayerSettingsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<K7.Shared.Dtos.TrackSelectionPreferencesDto> GetEffectiveTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.TrackSelectionPreferencesDto());
    public Task UpdateUserTrackSelectionPreferencesAsync(K7.Shared.Dtos.TrackSelectionPreferencesDto preferences, Guid? libraryId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResetUserTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<K7.Shared.Dtos.SyncPlayPreferencesDto> GetSyncPlayPreferencesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.SyncPlayPreferencesDto());
    public Task UpdateSyncPlayPreferencesAsync(K7.Shared.Dtos.SyncPlayPreferencesDto preferences, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockServerPreferencesService : IServerPreferencesService
{
    public Task<K7.Shared.Dtos.Home.HomeLayoutDto?> GetServerHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult<K7.Shared.Dtos.Home.HomeLayoutDto?>(null);
    public Task<K7.Shared.Dtos.Home.HomeLayoutDto> GetEffectiveServerHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.Home.HomeLayoutDto { Rows = [] });
    public Task UpdateServerHomeLayoutAsync(K7.Shared.Dtos.Home.HomeLayoutDto layout, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteServerHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<K7.Shared.Dtos.ServerFeatureFlagsDto> GetServerFeatureFlagsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.ServerFeatureFlagsDto());
    public Task UpdateServerFeatureFlagsAsync(K7.Shared.Dtos.ServerFeatureFlagsDto flags, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<K7.Shared.Dtos.MusicIntelligenceStatusDto> GetMusicIntelligenceStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.MusicIntelligenceStatusDto());
    public Task<IReadOnlyList<K7.Shared.Dtos.MusicMoodPresetDto>> GetMusicMoodPresetsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<K7.Shared.Dtos.MusicMoodPresetDto>>([]);
    public Task<K7.Shared.Dtos.VideoPlayerSettingsDto?> GetServerVideoPlayerSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult<K7.Shared.Dtos.VideoPlayerSettingsDto?>(null);
    public Task UpdateServerVideoPlayerSettingsAsync(K7.Shared.Dtos.VideoPlayerSettingsDto settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteServerVideoPlayerSettingsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<K7.Shared.Dtos.TrackSelectionPreferencesDto?> GetServerTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default) => Task.FromResult<K7.Shared.Dtos.TrackSelectionPreferencesDto?>(null);
    public Task UpdateServerTrackSelectionPreferencesAsync(K7.Shared.Dtos.TrackSelectionPreferencesDto preferences, Guid? libraryId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteServerTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockCollectionService : ICollectionService
{
    public Task<PaginatedListDto<LiteCollectionDto>?> GetCollectionsAsync(int pageNumber = 1, int pageSize = 20, MediaType? mediaType = null, bool? isPublic = null, LibraryItemOrderingOption? orderBy = null, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<LiteCollectionDto>?>(new PaginatedListDto<LiteCollectionDto>());
    public Task<CollectionDto?> GetCollectionAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CollectionDto?>(null);
    public Task<PaginatedListDto<CollectionItemDto>?> GetCollectionItemsAsync(Guid collectionId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<CollectionItemDto>?>(new PaginatedListDto<CollectionItemDto>());
    public Task<Guid> CreateCollectionAsync(CreateCollectionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
    public Task UpdateCollectionAsync(Guid id, UpdateCollectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteCollectionAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Guid> UploadCollectionCoverAsync(Guid collectionId, Stream stream, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task<Guid> SetCollectionCoverFromPictureAsync(Guid collectionId, Guid sourcePictureId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task RemoveCollectionCoverAsync(Guid collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Guid> AddCollectionItemAsync(Guid collectionId, Guid mediaId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
    public Task RemoveCollectionItemAsync(Guid collectionId, Guid itemId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockSearchService : ISearchService
{
    public Task<GlobalSearchResultDto?> GlobalSearchAsync(string q, int pageSize = 10, CancellationToken cancellationToken = default) => Task.FromResult<GlobalSearchResultDto?>(new GlobalSearchResultDto());
}

public sealed class MockConnectivityService : IConnectivityService
{
    public bool IsOnline => true;
    public bool IsWifi => true;
    public bool IsCellular => false;
#pragma warning disable CS0067
    public event Action<bool>? ConnectivityChanged;
#pragma warning restore CS0067
}

public sealed class MockPlaybackJournal : IPlaybackJournal
{
    public Task RecordProgressAsync(Guid mediaId, Guid indexedFileId, double position, double duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RecordCompletedAsync(Guid mediaId, Guid indexedFileId, double duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RecordSkippedAsync(Guid mediaId, Guid indexedFileId, double position, double duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RecordRatingAsync(Guid mediaId, int value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<PendingPlaybackEvent>> GetPendingEventsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingPlaybackEvent>>([]);
    public Task MarkSyncedAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockCastService : ICastService
{
    public bool IsAvailable => false;
    public bool IsCasting => false;
    public IReadOnlyList<CastDeviceInfo> DiscoveredDevices => [];
#pragma warning disable CS0067
    public event Action? StateChanged;
    public event Action<IReadOnlyList<CastDeviceInfo>>? DevicesDiscovered;
    public event Action<CastMediaStatus>? MediaStatusUpdated;
#pragma warning restore CS0067
    public Task StartDiscoveryAsync() => Task.CompletedTask;
    public Task StopDiscoveryAsync() => Task.CompletedTask;
    public Task CastAsync(CastMediaRequest request) => Task.CompletedTask;
    public Task StopCastingAsync() => Task.CompletedTask;
    public Task SendTransportCommandAsync(CastTransportCommand command) => Task.CompletedTask;
}

public sealed class MockCastOrchestrationService : ICastOrchestrationService
{
    public Task CastCurrentVideoAsync(CastDeviceInfo device, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CastCurrentAudioAsync(CastDeviceInfo device, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopCastingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockRemoteControlService : IRemoteControlService
{
    public bool IsControlling => false;
    public bool IsAudio => false;
    public bool IsCastSession => false;
    public Guid? TargetDeviceId => null;
    public string? TargetDeviceName => null;
    public RemotePlaybackState PlaybackState => RemotePlaybackState.Stopped;
    public double Position => 0;
    public double Duration => 0;
    public double Volume => 1;
    public int? SelectedAudioTrackIndex => null;
    public int? SelectedSubtitleTrackIndex => null;
    public IReadOnlyList<RemoteTrackInfoDto> AudioTracks => [];
    public IReadOnlyList<RemoteTrackInfoDto> SubtitleTracks => [];
    public string? Title => null;
    public string? Artist => null;
    public string? AlbumTitle => null;
    public string? CoverUrl => null;
    public Guid? MediaId => null;
    public Guid? IndexedFileId => null;
#pragma warning disable CS0067
    public event Action? SessionChanged;
    public event Action? StateChanged;
#pragma warning restore CS0067
    public void StartSession(Guid targetDeviceId, string targetDeviceName, RemotePlaybackRequestDto request) { }
    public void StartCastSession(string deviceName, bool isAudio, string? title, string? artist, string? albumTitle, string? coverUrl, double duration, double startPosition) { }
    public void EndSession() { }
    public Task SendPlayAsync() => Task.CompletedTask;
    public Task SendPauseAsync() => Task.CompletedTask;
    public Task SendStopAsync() => Task.CompletedTask;
    public Task SendSeekAsync(double position) => Task.CompletedTask;
    public Task SendVolumeAsync(double volume) => Task.CompletedTask;
    public Task SendAudioTrackAsync(int trackIndex) => Task.CompletedTask;
    public Task SendSubtitleTrackAsync(int trackIndex) => Task.CompletedTask;
}

public sealed class MockSyncPlayService : ISyncPlayService
{
    public bool IsInGroup { get; set; } = false;
    public bool IsEnabled { get; set; } = true;
    public bool ShowChat { get; set; } = true;
    public bool ShowReactions { get; set; } = true;

    public SyncPlayGroupDto? CurrentGroup { get; set; } = null;
    public IReadOnlyList<SyncPlayChatMessageDto> ChatMessages { get; set; } = new List<SyncPlayChatMessageDto>();
    public IReadOnlyList<SyncPlayOnlineUserDto> OnlineUsers { get; set; } = new List<SyncPlayOnlineUserDto>();

    public event Action? GroupUpdated;
    public event Action<SyncPlayCommandDto>? CommandReceived;
    public event Action<long, double>? PlayAtReceived;
    public event Action<double>? SeekCorrectionReceived;
    public event Action<SyncPlayChatMessageDto>? ChatMessageReceived;
    public event Action<SyncPlayReactionDto>? ReactionReceived;
    public event Action<string>? ErrorReceived;
    public event Action<SyncPlayInvitationDto>? InvitationReceived;
    public event Action? OnlineUsersUpdated;
    public event Action<SyncPlayInviteLinkDto>? InviteLinkReceived;
    public event Action? RejoinRequested;

    public Task CreateGroupAsync(Guid? mediaReferenceId = null, string? mediaTitle = null, double mediaDuration = 0, string? mediaCoverUrl = null, double initialPosition = 0, bool isPlaying = false)
    {
        IsInGroup = true;
        GroupUpdated?.Invoke();
        return Task.CompletedTask;
    }

    public Task JoinGroupAsync(Guid groupId, string? guestToken = null, string? guestDisplayName = null)
    {
        IsInGroup = true;
        GroupUpdated?.Invoke();
        return Task.CompletedTask;
    }

    public Task JoinViaInviteTokenAsync(string token, string? guestDisplayName = null)
    {
        IsInGroup = true;
        GroupUpdated?.Invoke();
        return Task.CompletedTask;
    }

    public Task LeaveGroupAsync()
    {
        IsInGroup = false;
        CurrentGroup = null;
        GroupUpdated?.Invoke();
        return Task.CompletedTask;
    }

    public Task AddToQueueAsync(Guid mediaReferenceId, string title, double duration, string? coverUrl) => Task.CompletedTask;
    public Task BulkAddToQueueAsync(IReadOnlyList<SyncPlayQueueItemDto> items) => Task.CompletedTask;
    public Task RemoveFromQueueAsync(Guid queueItemId) => Task.CompletedTask;
    public Task SetCurrentMediaAsync(Guid mediaReferenceId, string title, double duration, string? coverUrl) => Task.CompletedTask;

    public Task IssueCommandAsync(SyncPlayCommandType commandType, double? value = null) => Task.CompletedTask;
    public Task ReportPositionAsync(double position) => Task.CompletedTask;
    public Task ReportReadyAsync() => Task.CompletedTask;

    public Task SendChatAsync(string text) => Task.CompletedTask;
    public Task SendReactionAsync(string emoji) => Task.CompletedTask;

    public Task GenerateGuestTokenAsync() => Task.CompletedTask;
    public Task GetInviteLinkAsync() => Task.CompletedTask;
    public Task InviteUserAsync(string targetUserId) => Task.CompletedTask;
    public Task KickAsync(Guid targetDeviceId) => Task.CompletedTask;
    public Task RefreshOnlineUsersAsync() => Task.CompletedTask;

    public void RequestRejoin()
    {
        RejoinRequested?.Invoke();
    }

    public bool IsOwnMessage(Guid messageId)
    {
        return false;
    }

    public void TriggerGroupUpdated() => GroupUpdated?.Invoke();
    public void TriggerCommandReceived(SyncPlayCommandDto cmd) => CommandReceived?.Invoke(cmd);
    public void TriggerPlayAtReceived(long timestamp, double pos) => PlayAtReceived?.Invoke(timestamp, pos);
    public void TriggerSeekCorrectionReceived(double pos) => SeekCorrectionReceived?.Invoke(pos);
    public void TriggerChatMessageReceived(SyncPlayChatMessageDto msg) => ChatMessageReceived?.Invoke(msg);
    public void TriggerReactionReceived(SyncPlayReactionDto reaction) => ReactionReceived?.Invoke(reaction);
    public void TriggerErrorReceived(string err) => ErrorReceived?.Invoke(err);
    public void TriggerInvitationReceived(SyncPlayInvitationDto invite) => InvitationReceived?.Invoke(invite);
    public void TriggerOnlineUsersUpdated() => OnlineUsersUpdated?.Invoke();
    public void TriggerInviteLinkReceived(SyncPlayInviteLinkDto link) => InviteLinkReceived?.Invoke(link);
}

public sealed class MockSleepTimerService : ISleepTimerService
{
    public bool IsActive => false;

    public SleepTimerMode Mode => SleepTimerMode.Off;

    public TimeSpan Remaining => TimeSpan.Zero;

    public event Action? TimerChanged;
    public event Action? TimerExpired;

    public void Cancel()
    {
        return;
    }

    public void Start(SleepTimerMode mode, TimeSpan? duration = null)
    {
        TimerChanged?.Clone();
        TimerExpired?.Clone();
        return;
    }
}

public sealed class MockDownloadManager : IDownloadManager
{
#pragma warning disable CS0067
    public event Action<DownloadProgressInfo>? ProgressChanged;
    public event Action<DownloadCompletedInfo>? DownloadCompleted;
    public event Action<DownloadFailedInfo>? DownloadFailed;
#pragma warning restore CS0067

    public IReadOnlyList<DownloadQueueItem> Queue => [];

    public Task EnqueueAsync(DownloadRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CancelAsync(Guid downloadId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CancelAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockMusicIntelligenceClientService : IMusicIntelligenceClientService
{
    public Task<List<Guid>> GetSimilarTracksAsync(Guid trackId, int count = 20, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Guid>());

    public Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Guid>());

    public Task<List<Guid>> GetSuggestionsAsync(IEnumerable<Guid> recentTrackIds, int count = 20, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Guid>());

    public Task<List<Guid>> CreateSmartPlaylistAsync(string prompt, int count = 30, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Guid>());

    public Task<List<Guid>> SearchTracksBySonicTextAsync(string query, int count = 50, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Guid>());

    public Task<List<Guid>> SearchTracksByLyricsAsync(string query, int count = 50, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Guid>());
}
