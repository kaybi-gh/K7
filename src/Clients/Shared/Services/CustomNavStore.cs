using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Interfaces;
using K7.Shared.Json;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;

public sealed class CustomNavStore : ICustomNavStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = K7JsonSerializerOptions.CreateDefault();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICustomNavHubEvents _hubEvents;
    private readonly IDeviceStorageService? _storage;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Task? _reloadTask;
    private string? _userId;
    private string? _fingerprint;
    private bool _serverLoaded;
    private bool _backgroundStarted;

    public CustomNavStore(
        IServiceScopeFactory scopeFactory,
        ICustomNavHubEvents hubEvents,
        IDeviceStorageService? storage = null)
    {
        _scopeFactory = scopeFactory;
        _hubEvents = hubEvents;
        _storage = storage;
        _hubEvents.CustomNavLayoutUpdated += OnCustomNavLayoutUpdated;
    }

    public event Action? Changed;

    public bool IsLoaded { get; private set; }

    public CustomNavLayoutDto Layout { get; private set; } = CustomNavLayoutDto.Disabled();

    public IReadOnlyList<LibraryGroupDto> Groups { get; private set; } = [];

    public IReadOnlyList<LiteCollectionDto> Collections { get; private set; } = [];

    public IReadOnlyList<LitePlaylistDto> Playlists { get; private set; } = [];

    public GeneralPreferencesDto GeneralPreferences { get; private set; } = new();

    public void BindUser(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            var hadData = IsLoaded || Layout.Enabled;
            _userId = null;
            _serverLoaded = false;
            _backgroundStarted = false;
            if (!hadData)
                return;

            ClearMemory();
            Changed?.Invoke();
            return;
        }

        if (string.Equals(_userId, userId, StringComparison.Ordinal))
            return;

        _userId = userId;
        _serverLoaded = false;
        _backgroundStarted = false;

        if (TryHydrate(userId))
        {
            Changed?.Invoke();
            return;
        }

        if (IsLoaded || Layout.Enabled || Groups.Count > 0)
        {
            ClearMemory();
            Changed?.Invoke();
        }
    }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        _serverLoaded ? Task.CompletedTask : ReloadAsync(cancellationToken);

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_reloadTask is { IsCompleted: false })
            return _reloadTask;

        _reloadTask = ReloadCoreAsync(cancellationToken);
        return _reloadTask;
    }

    public void RefreshInBackground()
    {
        if (_serverLoaded || string.IsNullOrEmpty(_userId) || _backgroundStarted)
            return;

        _backgroundStarted = true;
        _ = ReloadAsync();
    }

    public void Invalidate()
    {
        _userId = null;
        _serverLoaded = false;
        _backgroundStarted = false;
        ClearMemory();
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _hubEvents.CustomNavLayoutUpdated -= OnCustomNavLayoutUpdated;
        _gate.Dispose();
    }

    private async Task ReloadCoreAsync(CancellationToken cancellationToken)
    {
        var raise = false;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var preferences = scope.ServiceProvider.GetRequiredService<IUserPreferencesService>();
            var libraries = scope.ServiceProvider.GetRequiredService<ILibraryService>();
            var collections = scope.ServiceProvider.GetRequiredService<ICollectionService>();
            var playlists = scope.ServiceProvider.GetRequiredService<IPlaylistService>();

            CustomNavLayoutDto? layout = null;
            try
            {
                layout = await preferences.GetCustomNavLayoutAsync(cancellationToken);
            }
            catch
            {
                raise = ApplyUnavailable();
            }

            if (layout is not null)
            {
                IReadOnlyList<LibraryGroupDto> groups;
                try
                {
                    groups = await libraries.GetLibraryGroupsAsync(cancellationToken);
                }
                catch
                {
                    groups = Groups;
                }

                IReadOnlyList<LiteCollectionDto> collectionItems;
                try
                {
                    var page = await collections.GetCollectionsAsync(1, 100, cancellationToken: cancellationToken);
                    collectionItems = page?.Items?.ToList() ?? [];
                }
                catch
                {
                    collectionItems = Collections;
                }

                IReadOnlyList<LitePlaylistDto> playlistItems;
                try
                {
                    var page = await playlists.GetPlaylistsAsync(1, 100, cancellationToken: cancellationToken);
                    playlistItems = page?.Items?.ToList() ?? [];
                }
                catch
                {
                    playlistItems = Playlists;
                }

                GeneralPreferencesDto general;
                try
                {
                    general = await preferences.GetEffectiveGeneralPreferencesAsync(cancellationToken);
                }
                catch
                {
                    general = GeneralPreferences;
                }

                var nextFingerprint = Fingerprint(layout, groups, collectionItems, playlistItems, general);
                raise = nextFingerprint != _fingerprint;
                Layout = layout;
                Groups = groups;
                Collections = collectionItems;
                Playlists = playlistItems;
                GeneralPreferences = general;
                IsLoaded = true;
                _serverLoaded = true;
                _fingerprint = nextFingerprint;
                Persist();
            }
        }
        finally
        {
            _gate.Release();
        }

        if (raise)
            Changed?.Invoke();
    }

    private bool ApplyUnavailable()
    {
        _serverLoaded = false;
        if (IsLoaded)
            return false;

        ClearMemory();
        IsLoaded = true;
        _serverLoaded = true;
        return true;
    }

    private bool TryHydrate(string userId)
    {
        var entry = ReadCache();
        if (entry?.Layout is null || !string.Equals(entry.UserId, userId, StringComparison.Ordinal))
            return false;

        Layout = entry.Layout;
        Groups = entry.Groups ?? [];
        Collections = entry.Collections ?? [];
        Playlists = entry.Playlists ?? [];
        GeneralPreferences = entry.GeneralPreferences ?? new GeneralPreferencesDto();
        IsLoaded = true;
        _fingerprint = Fingerprint(Layout, Groups, Collections, Playlists, GeneralPreferences);
        return true;
    }

    private void Persist()
    {
        if (_storage is null || string.IsNullOrEmpty(_userId))
            return;

        var entry = new CustomNavCacheEntry
        {
            UserId = _userId,
            Layout = Layout,
            Groups = Groups.ToList(),
            Collections = Collections.ToList(),
            Playlists = Playlists.ToList(),
            GeneralPreferences = GeneralPreferences
        };

        try
        {
            _storage.Set(PreferenceKeys.CUSTOM_NAV_CACHE, JsonSerializer.Serialize(entry, JsonOptions));
        }
        catch
        {
            // A full disk or a private-mode storage failure must not drop the in-memory layout.
        }
    }

    private CustomNavCacheEntry? ReadCache()
    {
        if (_storage is null)
            return null;

        var json = _storage.Get(PreferenceKeys.CUSTOM_NAV_CACHE);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CustomNavCacheEntry>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void ClearMemory()
    {
        IsLoaded = false;
        Layout = CustomNavLayoutDto.Disabled();
        Groups = [];
        Collections = [];
        Playlists = [];
        GeneralPreferences = new GeneralPreferencesDto();
        _fingerprint = null;
    }

    private void OnCustomNavLayoutUpdated(CustomNavLayoutDto layout)
    {
        Layout = layout;
        IsLoaded = true;
        _serverLoaded = true;
        _fingerprint = Fingerprint(Layout, Groups, Collections, Playlists, GeneralPreferences);
        Persist();
        Changed?.Invoke();
    }

    private static string Fingerprint(
        CustomNavLayoutDto layout,
        IReadOnlyList<LibraryGroupDto> groups,
        IReadOnlyList<LiteCollectionDto> collections,
        IReadOnlyList<LitePlaylistDto> playlists,
        GeneralPreferencesDto preferences) =>
        JsonSerializer.Serialize(new CustomNavCacheEntry
        {
            Layout = layout,
            Groups = groups.ToList(),
            Collections = collections.ToList(),
            Playlists = playlists.ToList(),
            GeneralPreferences = preferences
        }, JsonOptions);

    private sealed class CustomNavCacheEntry
    {
        public string UserId { get; set; } = "";

        public CustomNavLayoutDto? Layout { get; set; }

        public List<LibraryGroupDto>? Groups { get; set; }

        public List<LiteCollectionDto>? Collections { get; set; }

        public List<LitePlaylistDto>? Playlists { get; set; }

        public GeneralPreferencesDto? GeneralPreferences { get; set; }
    }
}
