using System.Security.Claims;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Diagnostics;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.Users;
using K7.Shared.Dtos.Requests;
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
    public Task<bool> SwitchToUserAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(true);
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
    public Task<bool> GetHdrSupportAsync() => Task.FromResult(false);
    public Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync() => Task.FromResult(new CreateDeviceRequest { PlaybackCapabilities = new CreateDeviceRequestPlaybackCapibilities() });
}

public sealed class MockDeviceStorageService : IDeviceStorageService
{
    public T? Get<T>(PreferenceKey<T> key, T? defaultValue = default) => defaultValue;
    public void Set<T>(PreferenceKey<T> key, T value) { }
    public void Remove<T>(PreferenceKey<T> key) { }
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
    public event Func<PlayerSource, double, Task>? CrossfadeRequested;
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
    public bool AdaptiveCrossfade => false;
    public double CrossfadeDuration => 0;
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

    public Task PlayTracksAsync(IEnumerable<AudioQueueItem> tracks, int startIndex = 0, CancellationToken cancellationToken = default)
    {
        _queue = [.. tracks];
        _currentIndex = startIndex;
        if (_queue.Count > 0) { SetCurrentTrack(_queue[_currentIndex]); Play(); }
        return Task.CompletedTask;
    }

    public void AddToQueue(AudioQueueItem track) { _queue.Add(track); QueueChanged?.Invoke(); }
    public void AddToQueueNext(AudioQueueItem track) { _queue.Insert(_currentIndex + 1, track); QueueChanged?.Invoke(); }
    public void RemoveFromQueue(int index) { if (index >= 0 && index < _queue.Count) { _queue.RemoveAt(index); QueueChanged?.Invoke(); } }
    public void ClearQueue() { _queue.Clear(); QueueChanged?.Invoke(); }

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
    public Task<List<MediaFormatDto>> GetMediaFormatsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<MediaFormatDto>());
    public Task<MovieDto?> GetMovieAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<MovieDto?>(null);
    public Task<MediaDto?> GetMediaAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<MediaDto?>(null);
    public Task<PaginatedListDto<LiteMediaDto>?> GetLiteMediasAsync(GetMediasWithPaginationQuery query, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<LiteMediaDto>?>(null);
    public Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PersonDto?>(null);
    public Task<PaginatedListDto<PersonDto>?> GetPersonsAsync(GetPersonsWithPaginationQuery query, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<PersonDto>?>(null);
    public Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year = null, string? providerId = null, MediaType? mediaType = null, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MetadataSearchResult>());
    public Task ReidentifyIndexedFileAsync(Guid id, ReidentifyIndexedFileRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReidentifyMediaAsync(Guid id, ReidentifyMediaRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RefreshMediaMetadataAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<LiteSerieEpisodeDto?> GetNextEpisodeAsync(Guid serieId, Guid currentEpisodeId, CancellationToken cancellationToken = default) => Task.FromResult<LiteSerieEpisodeDto?>(null);
}

public sealed class MockLibraryService : ILibraryService
{
    public Task<List<LibraryDto>> GetLibrariesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<LibraryDto>());
    public Task<Guid> CreateLibraryAsync(CreateLibraryRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task UpdateLibraryAsync(Guid id, UpdateLibraryRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteLibraryAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task IndexLibraryFilesAsync(Guid libraryId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<DirectoryContentDto?> GetDirectoriesAsync(string? path = null, CancellationToken cancellationToken = default) => Task.FromResult<DirectoryContentDto?>(null);
    public Task<List<MetadataProviderInfoDto>> GetMetadataProvidersAsync(LibraryMediaType? mediaType = null, CancellationToken cancellationToken = default) => Task.FromResult(new List<MetadataProviderInfoDto>());
    public Task<Guid> UploadLibraryCoverAsync(Guid libraryId, Stream stream, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task<Guid> SetLibraryCoverFromPictureAsync(Guid libraryId, Guid sourcePictureId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task<List<LibraryPictureDto>> GetLibraryPicturesAsync(Guid libraryId, CancellationToken cancellationToken = default) => Task.FromResult(new List<LibraryPictureDto>());
}

public sealed class MockPlaylistService : IPlaylistService
{
    public Task<PaginatedListDto<LitePlaylistDto>?> GetPlaylistsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<LitePlaylistDto>?>(null);
    public Task<PlaylistDto?> GetPlaylistAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PlaylistDto?>(null);
    public Task<PaginatedListDto<PlaylistItemDto>?> GetPlaylistItemsAsync(Guid playlistId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedListDto<PlaylistItemDto>?>(null);
    public Task<Guid> CreatePlaylistAsync(CreatePlaylistRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task UpdatePlaylistAsync(Guid id, UpdatePlaylistRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeletePlaylistAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Guid> AddPlaylistItemAsync(Guid playlistId, Guid mediaId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
    public Task RemovePlaylistItemAsync(Guid playlistId, Guid itemId, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
    public Task ReportPlaybackProgressAsync(Guid mediaId, Guid sessionId, Guid referenceId, double position, double duration, int state, Guid? deviceId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
}

public sealed class MockRatingService : IRatingService
{
    public Task RateMediaAsync(Guid mediaId, int value, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockBackgroundTaskService : IBackgroundTaskService
{
    public Task<PaginatedListDto<BackgroundTaskDto>> GetBackgroundTasksAsync(int pageNumber = 1, int pageSize = 20, IReadOnlyCollection<BackgroundTaskStatus>? statuses = null, IReadOnlyCollection<string>? names = null, CancellationToken cancellationToken = default) => Task.FromResult(new PaginatedListDto<BackgroundTaskDto>());
    public Task<BackgroundTaskDto> GetBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(new BackgroundTaskDto { Id = Guid.Empty, Name = "" });
    public Task DeleteBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<BackgroundTaskSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new BackgroundTaskSettingsDto { WorkerCount = 1, ConcurrencyGroups = [] });
    public Task UpdateSettingsAsync(UpdateBackgroundTaskSettingsRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<BackgroundTaskSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default) => Task.FromResult(new BackgroundTaskSummaryDto { TotalCount = 0, StatusCounts = [], TaskTypeCounts = [] });
}

public sealed class MockDiagnosticsService : IDiagnosticsService
{
    public Task<List<LibraryHealthSummaryDto>> GetDiagnosticsSummaryAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<LibraryHealthSummaryDto>());
    public Task<PaginatedListDto<DiagnosticItemDto>> GetDiagnosticItemsAsync(Guid? libraryId = null, DiagnosticEntityType? entityType = null, DiagnosticIssue? issue = null, IReadOnlyCollection<DiagnosticIssue>? issues = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Task.FromResult(new PaginatedListDto<DiagnosticItemDto>());
    public Task<int> FixDiagnosticItemsAsync(IReadOnlyList<Guid> entityIds, DiagnosticFixAction action, CancellationToken cancellationToken = default) => Task.FromResult(0);
}

public sealed class MockServerInfoService : IServerInfoService
{
    public Task<ServerInfoDto?> GetServerInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult<ServerInfoDto?>(null);
    public Task<AuthenticationInfoDto?> GetAuthenticationInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult<AuthenticationInfoDto?>(null);
    public Task<MusicStatsDto?> GetMusicStatsAsync(CancellationToken cancellationToken = default) => Task.FromResult<MusicStatsDto?>(null);
    public Task<WatchStatsDto?> GetWatchStatsAsync(string? mediaType = null, string period = "month", CancellationToken cancellationToken = default) => Task.FromResult<WatchStatsDto?>(null);
    public Task<PlaybackHistoryPageDto?> GetPlaybackHistoryAsync(int page = 1, int pageSize = 25, string? mediaType = null, CancellationToken cancellationToken = default) => Task.FromResult<PlaybackHistoryPageDto?>(null);
    public Task<List<MediaDto>?> GetMusicRadioAsync(string type, Guid? seedTrackId = null, Guid? seedArtistId = null, string? moodPreset = null, int limit = 50, CancellationToken cancellationToken = default) => Task.FromResult<List<MediaDto>?>(null);
    public Task UpdateDefaultLanguageAsync(string language, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<List<ActiveStreamDto>?> GetActiveStreamsAsync(CancellationToken cancellationToken = default) => Task.FromResult<List<ActiveStreamDto>?>(null);
}

public sealed class MockUserPreferencesService : IUserPreferencesService
{
    public Task<IReadOnlyList<Guid>> GetSelfLibraryExclusionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    public Task UpdateSelfLibraryExclusionsAsync(K7.Shared.Dtos.Requests.UpdateSelfLibraryExclusionsRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<K7.Shared.Dtos.Home.HomeLayoutDto> GetHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.Home.HomeLayoutDto { Rows = [] });
    public Task UpdateHomeLayoutAsync(K7.Shared.Dtos.Home.HomeLayoutDto layout, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResetHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MockServerPreferencesService : IServerPreferencesService
{
    public Task<K7.Shared.Dtos.Home.HomeLayoutDto?> GetServerHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult<K7.Shared.Dtos.Home.HomeLayoutDto?>(null);
    public Task<K7.Shared.Dtos.Home.HomeLayoutDto> GetEffectiveServerHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(new K7.Shared.Dtos.Home.HomeLayoutDto { Rows = [] });
    public Task UpdateServerHomeLayoutAsync(K7.Shared.Dtos.Home.HomeLayoutDto layout, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteServerHomeLayoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
