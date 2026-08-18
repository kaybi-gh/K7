using System.Net.Http;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Services;

public sealed class HomeFeedStore : IHomeFeedStore, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly K7HubClient _hubClient;
    private readonly MediaCacheStore _cacheStore;
    private readonly IDeviceService _deviceService;
    private readonly IConnectivityService _connectivity;
    private readonly ISharedProfileSessionService _sharedProfileSession;
    private readonly ILogger<HomeFeedStore> _logger;

    private readonly List<HomeFeedRow> _rows = [];
    private readonly object _sync = new();
    private CancellationTokenSource? _picturesRefreshCts;
    private CancellationTokenSource? _membershipRefreshCts;
    private CancellationTokenSource? _continueWatchingRefreshCts;
    private CancellationTokenSource? _watchStateRefreshCts;
    private Task? _loadTask;
    private int _catalogRefreshGeneration;
    private int _loadGeneration;
    private bool _isLoaded;
    private bool _isTv;
    private bool _hubHandlersRegistered;
    private bool _pendingRefresh;
    private bool _hasFeedContext;
    private Guid? _feedSharedProfileId;
    private string? _feedIdentityUserId;

    private static readonly TimeSpan ContinueWatchingRefreshDelay = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan WatchStateRefreshDelay = TimeSpan.FromSeconds(1.5);

    public event Action? Changed;

    public bool IsLoading { get; private set; }

    public bool IsLoaded => _isLoaded;

    public bool IsOffline { get; private set; }

    public bool CanTrackProgress { get; private set; }

    public IReadOnlyList<HomeFeedRow> Rows
    {
        get
        {
            lock (_sync)
            {
                return _rows.ToList();
            }
        }
    }

    public HomeFeedStore(
        IServiceScopeFactory scopeFactory,
        K7HubClient hubClient,
        MediaCacheStore cacheStore,
        IDeviceService deviceService,
        IConnectivityService connectivity,
        ISharedProfileSessionService sharedProfileSession,
        ILogger<HomeFeedStore> logger)
    {
        _scopeFactory = scopeFactory;
        _hubClient = hubClient;
        _cacheStore = cacheStore;
        _deviceService = deviceService;
        _connectivity = connectivity;
        _sharedProfileSession = sharedProfileSession;
        _logger = logger;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        _sharedProfileSession.ActiveGroupChanged += OnActiveGroupChanged;
    }

    public Task ResetAndReloadAsync(CancellationToken cancellationToken = default)
    {
        InvalidateLoadedState();
        return EnsureLoadedAsync(CanTrackProgress, _feedIdentityUserId, cancellationToken);
    }

    public Task EnsureLoadedAsync(
        bool canTrackProgress,
        string? identityUserId,
        CancellationToken cancellationToken = default)
    {
        var capabilityEnabled = !CanTrackProgress && canTrackProgress;
        CanTrackProgress = canTrackProgress;

        Task loadTask;
        lock (_sync)
        {
            var currentProfileId = _sharedProfileSession.ActiveGroupId;
            if (_hasFeedContext && !IsSameFeedContext(identityUserId, currentProfileId))
            {
                InvalidateLoadedStateCore();
                InvalidateCache();
            }

            BindFeedContext(identityUserId, currentProfileId);

            if (_isLoaded && _loadTask is { IsCompletedSuccessfully: true })
            {
                // Capability flipped on after a previous load that skipped CW rows.
                if (capabilityEnabled)
                    loadTask = LoadSkippedContinueWatchingAsync(cancellationToken);
                else
                    loadTask = Task.CompletedTask;
            }
            else
            {
                if (_loadTask is { IsFaulted: true } or { IsCanceled: true })
                    _loadTask = null;

                _loadTask ??= LoadAsync(cancellationToken);
                loadTask = _loadTask;
            }
        }

        return loadTask;
    }

    public void RemoveMedia(string mediaId)
    {
        lock (_sync)
        {
            foreach (var row in _rows)
                row.Items.RemoveAll(x => x.Id == mediaId);
        }

        NotifyChanged();
    }

    public void RemoveMediaAndChildren(string mediaId)
    {
        lock (_sync)
        {
            foreach (var row in _rows)
                row.Items.RemoveAll(x => x.Id == mediaId || x.ParentId == mediaId);
        }

        NotifyChanged();
    }

    public void InvalidateCache() => _cacheStore.InvalidateByPrefix("home-feed");

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_isLoaded || IsLoading || IsOffline)
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);
        await RefreshAllRowsAsync();
        NotifyChanged();
    }

    public async Task RefreshContinueWatchingAsync(CancellationToken cancellationToken = default)
    {
        if (!_isLoaded || IsLoading || IsOffline)
        {
            _pendingRefresh = true;
            return;
        }

        if (!CanTrackProgress)
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);
        InvalidateCache();
        var ok = await RefreshContinueWatchingRowsAsync();
        _pendingRefresh = !ok;
        NotifyChanged();
    }

    public void Dispose()
    {
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _sharedProfileSession.ActiveGroupChanged -= OnActiveGroupChanged;
        UnregisterHubHandlers();
        CancelBackgroundRefreshes();
    }

    private void OnActiveGroupChanged()
    {
        if (!_hasFeedContext || IsSameFeedContext(_feedIdentityUserId, _sharedProfileSession.ActiveGroupId))
            return;

        _ = ReloadAfterSharedProfileChangedAsync();
    }

    private async Task ReloadAfterSharedProfileChangedAsync()
    {
        try
        {
            await EnsureLoadedAsync(CanTrackProgress, _feedIdentityUserId);
        }
        catch
        {
            // Best effort; UI will reflect store state on next Changed.
        }
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        if (!isOnline || !IsOffline)
            return;

        lock (_sync)
        {
            _isLoaded = false;
            _loadTask = null;
            IsOffline = false;
        }

        _ = ReloadAfterConnectivityRestoredAsync();
    }

    private void FailTransientLoad()
    {
        IsLoading = false;
        lock (_sync)
        {
            _isLoaded = false;
            _loadTask = null;
        }

        NotifyChanged();
    }

    private async Task ReloadAfterConnectivityRestoredAsync()
    {
        try
        {
            await EnsureLoadedAsync(CanTrackProgress, _feedIdentityUserId);
        }
        catch
        {
            // Best effort; UI will reflect store state on next Changed.
        }
    }

    private async Task LoadSkippedContinueWatchingAsync(CancellationToken cancellationToken = default)
    {
        if (!CanTrackProgress)
            return;

        InvalidateCache();
        var ok = await RefreshContinueWatchingRowsAsync();
        _pendingRefresh = !ok;
        NotifyChanged();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        RegisterHubHandlers();

        var loadGeneration = Volatile.Read(ref _loadGeneration);
        var profileAtStart = _feedSharedProfileId;
        var identityAtStart = _feedIdentityUserId;

        IsLoading = true;
        IsOffline = false;
        NotifyChanged();

        _isTv = await _deviceService.GetDeviceTypeAsync() == DeviceType.TV;

        if (IsLoadSuperseded(loadGeneration, profileAtStart, identityAtStart))
            return;

        HomeLayoutDto layout;
        try
        {
            layout = await LoadHomeLayoutWithRetryAsync(cancellationToken);
        }
        catch (HttpRequestException) when (!_connectivity.IsOnline)
        {
            CompleteLoad(loadGeneration, profileAtStart, identityAtStart, offline: true);
            return;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && !_connectivity.IsOnline)
        {
            CompleteLoad(loadGeneration, profileAtStart, identityAtStart, offline: true);
            return;
        }
        catch (HttpRequestException)
        {
            if (!IsLoadSuperseded(loadGeneration, profileAtStart, identityAtStart))
                FailTransientLoad();
            return;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!IsLoadSuperseded(loadGeneration, profileAtStart, identityAtStart))
                FailTransientLoad();
            return;
        }
        catch
        {
            layout = new HomeLayoutDto { Rows = [] };
        }

        if (IsLoadSuperseded(loadGeneration, profileAtStart, identityAtStart))
            return;

        var rowConfigs = layout.Rows
            .Where(r => r.IsVisible)
            .OrderBy(r => r.Order)
            .ToList();

        lock (_sync)
        {
            if (IsLoadSuperseded(loadGeneration, profileAtStart, identityAtStart))
                return;

            _rows.Clear();
            foreach (var config in rowConfigs)
                _rows.Add(new HomeFeedRow { Config = config });
        }

        var rowsSnapshot = GetRowsSnapshot();
        var tasks = rowsSnapshot
            .Where(r => !r.Config.ContinueWatching || CanTrackProgress)
            .Select(r => LoadRowAsync(r.Config, r.Items, cancellationToken))
            .ToList();

        await Task.WhenAll(tasks);

        if (IsLoadSuperseded(loadGeneration, profileAtStart, identityAtStart))
            return;

        CompleteLoad(loadGeneration, profileAtStart, identityAtStart, offline: false);
        AppReadySignal.Signal();

        if (_pendingRefresh)
        {
            Interlocked.Increment(ref _catalogRefreshGeneration);
            InvalidateCache();
            var rows = GetRowsSnapshot();
            var results = await Task.WhenAll(rows.Select(RefreshRowAsync));
            _pendingRefresh = results.Any(ok => !ok);
            NotifyChanged();
        }
    }

    private bool IsLoadSuperseded(int loadGeneration, Guid? profileAtStart, string? identityAtStart) =>
        loadGeneration != Volatile.Read(ref _loadGeneration)
        || _feedSharedProfileId != profileAtStart
        || !string.Equals(_feedIdentityUserId, identityAtStart, StringComparison.Ordinal);

    private void CompleteLoad(int loadGeneration, Guid? profileId, string? identityUserId, bool offline)
    {
        if (IsLoadSuperseded(loadGeneration, profileId, identityUserId))
            return;

        IsOffline = offline;
        IsLoading = false;
        _isLoaded = true;
        NotifyChanged();
    }

    private void InvalidateLoadedState()
    {
        lock (_sync)
        {
            InvalidateLoadedStateCore();
        }

        IsOffline = false;
        InvalidateCache();
    }

    private void InvalidateLoadedStateCore()
    {
        Interlocked.Increment(ref _loadGeneration);
        Interlocked.Increment(ref _catalogRefreshGeneration);
        CancelBackgroundRefreshes();
        _isLoaded = false;
        _loadTask = null;
        _rows.Clear();
        IsLoading = false;
    }

    private void BindFeedContext(string? identityUserId, Guid? profileId)
    {
        _feedIdentityUserId = identityUserId;
        _feedSharedProfileId = profileId;
        _hasFeedContext = true;
    }

    private bool IsSameFeedContext(string? identityUserId, Guid? profileId) =>
        string.Equals(_feedIdentityUserId, identityUserId, StringComparison.Ordinal)
        && _feedSharedProfileId == profileId;

    private void CancelBackgroundRefreshes()
    {
        CancelAndDispose(ref _picturesRefreshCts);
        CancelAndDispose(ref _membershipRefreshCts);
        CancelAndDispose(ref _continueWatchingRefreshCts);
        CancelAndDispose(ref _watchStateRefreshCts);
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void RegisterHubHandlers()
    {
        if (_hubHandlersRegistered)
            return;

        _hubHandlersRegistered = true;
        _hubClient.MediaBatchAdded += OnMediaBatchAdded;
        _hubClient.MediaIndexedFilesUpdated += OnMediaIndexedFilesUpdated;
        _hubClient.LibraryScanCompleted += OnLibraryScanCompleted;
        _hubClient.MediaMetadataRefreshed += OnMediaMetadataRefreshed;
        _hubClient.MediaPicturesUpdated += OnMediaPicturesUpdated;
        _hubClient.ProgressUpdated += OnProgressUpdated;
        _cacheStore.HomeFeedInvalidated += OnHomeFeedInvalidated;
    }

    private void UnregisterHubHandlers()
    {
        if (!_hubHandlersRegistered)
            return;

        _hubHandlersRegistered = false;
        _hubClient.MediaBatchAdded -= OnMediaBatchAdded;
        _hubClient.MediaIndexedFilesUpdated -= OnMediaIndexedFilesUpdated;
        _hubClient.LibraryScanCompleted -= OnLibraryScanCompleted;
        _hubClient.MediaMetadataRefreshed -= OnMediaMetadataRefreshed;
        _hubClient.MediaPicturesUpdated -= OnMediaPicturesUpdated;
        _hubClient.ProgressUpdated -= OnProgressUpdated;
        _cacheStore.HomeFeedInvalidated -= OnHomeFeedInvalidated;
    }

    private void OnProgressUpdated(Guid mediaId, double progressPercentage, bool isCompleted, MediaType mediaType)
    {
        if (!_isLoaded || IsLoading || IsOffline)
        {
            _pendingRefresh = true;
            return;
        }

        // Music is never Keep Watching material; skip feed churn from audio progress ticks.
        if (mediaType is MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist)
            return;

        var id = mediaId.ToString();
        var changed = false;

        lock (_sync)
        {
            foreach (var row in _rows)
            {
                for (var i = 0; i < row.Items.Count; i++)
                {
                    var item = row.Items[i];
                    if (item.Id != id && item.ParentId != id)
                        continue;

                    // Mutate in place so Blazor keeps the same card instance (@key) and posters do not blink.
                    item.Progress = progressPercentage;
                    item.Watched = isCompleted;
                    changed = true;
                }
            }
        }

        if (changed)
            NotifyChanged();

        // Membership may still change (enter/leave Keep Watching). Patch existing bars live;
        // debounce membership refresh so progress ticks do not storm the feed API.
        // Completion still refreshes immediately so the card leaves CW without waiting.
        if (GetRowsSnapshot().Any(r => r.Config.ContinueWatching))
        {
            var delay = isCompleted ? TimeSpan.Zero : ContinueWatchingRefreshDelay;
            ScheduleContinueWatchingRefresh(delay);
        }

        // Soft re-fetch non-CW rows on completion or series progress so Watched / episode
        // aggregates catch up without chasing every movie progress tick.
        if (isCompleted || mediaType is MediaType.SerieEpisode or MediaType.SerieSeason or MediaType.Serie)
        {
            ScheduleWatchStateRefresh(
                mediaId,
                isCompleted ? TimeSpan.Zero : WatchStateRefreshDelay);
        }
    }

    private void ScheduleContinueWatchingRefresh(TimeSpan delay)
    {
        _continueWatchingRefreshCts?.Cancel();
        _continueWatchingRefreshCts?.Dispose();
        _continueWatchingRefreshCts = new CancellationTokenSource();
        var token = _continueWatchingRefreshCts.Token;
        var generation = Volatile.Read(ref _loadGeneration);

        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, token);

                if (generation != Volatile.Read(ref _loadGeneration))
                    return;

                InvalidateCache();
                var ok = await RefreshContinueWatchingRowsAsync();
                if (!ok)
                    _pendingRefresh = true;
                if (!token.IsCancellationRequested && generation == Volatile.Read(ref _loadGeneration))
                    NotifyChanged();
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void ScheduleWatchStateRefresh(Guid mediaId, TimeSpan delay)
    {
        _watchStateRefreshCts?.Cancel();
        _watchStateRefreshCts?.Dispose();
        _watchStateRefreshCts = new CancellationTokenSource();
        var token = _watchStateRefreshCts.Token;
        var id = mediaId.ToString();
        var generation = Volatile.Read(ref _loadGeneration);

        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, token);

                if (generation != Volatile.Read(ref _loadGeneration))
                    return;

                InvalidateCache();
                var rows = GetRowsSnapshot()
                    .Where(r => !r.Config.ContinueWatching
                        && r.Items.Any(i => i.Id == id || i.ParentId == id))
                    .ToList();
                await Task.WhenAll(rows.Select(RefreshRowAsync));
                if (!token.IsCancellationRequested && generation == Volatile.Read(ref _loadGeneration))
                    NotifyChanged();
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void OnMediaMetadataRefreshed(Guid mediaId) =>
        ScheduleCatalogRefreshIfAffected(mediaId);

    private void OnMediaPicturesUpdated(Guid mediaId) =>
        ScheduleCatalogRefreshIfAffected(mediaId);

    private void ScheduleCatalogRefreshIfAffected(Guid mediaId)
    {
        if (!_isLoaded || IsLoading || IsOffline)
            return;

        if (!GetRowsSnapshot().Any(r => IsCardAffected(r.Items, mediaId)))
            return;

        SchedulePicturesRefresh();
    }

    private void SchedulePicturesRefresh()
    {
        _picturesRefreshCts?.Cancel();
        _picturesRefreshCts?.Dispose();
        _picturesRefreshCts = new CancellationTokenSource();
        var token = _picturesRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                Interlocked.Increment(ref _catalogRefreshGeneration);
                InvalidateCache();
                await RefreshAllRowsAsync();
                NotifyChanged();
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void OnHomeFeedInvalidated()
    {
        if (!_isLoaded || IsLoading || IsOffline)
        {
            _pendingRefresh = true;
            return;
        }

        Interlocked.Increment(ref _catalogRefreshGeneration);
        _ = RefreshAllRowsAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
                _pendingRefresh = true;
            NotifyChanged();
        }, TaskScheduler.Default);
    }

    private void OnMediaBatchAdded(List<MediaBatchItem> items)
    {
        if (!_isLoaded || IsLoading || IsOffline || items.Count == 0)
            return;

        if (!GetRowsSnapshot().Any(r => RowMightBeAffectedByBatch(r, items)))
            return;

        // New catalog membership: debounce so rapid CreateMedia batches coalesce into one refresh.
        ScheduleCatalogMembershipRefresh(refreshContinueWatching: false, batchItems: items);
    }

    private void OnMediaIndexedFilesUpdated(Guid mediaId, Guid libraryId)
    {
        if (!_isLoaded || IsLoading || IsOffline || !RowMightBeAffectedByLibrary(libraryId))
            return;

        ScheduleCatalogMembershipRefresh(refreshContinueWatching: true);
    }

    private void OnLibraryScanCompleted(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount)
    {
        if (!_isLoaded || IsLoading || IsOffline)
        {
            _pendingRefresh = true;
            return;
        }

        // Dynamic default home layout adds a "Newly added in ..." row per library group. Refreshing
        // existing row items is not enough when the library is new: global rows (null LibraryIds)
        // make RowMightBeAffectedByLibrary true without ever creating that feed.
        if (!HasRowTargetingLibrary(libraryId))
        {
            ScheduleLayoutReload();
            return;
        }

        ScheduleCatalogMembershipRefresh(refreshContinueWatching: false);
    }

    private void ScheduleCatalogMembershipRefresh(bool refreshContinueWatching, List<MediaBatchItem>? batchItems = null)
    {
        _membershipRefreshCts?.Cancel();
        _membershipRefreshCts?.Dispose();
        _membershipRefreshCts = new CancellationTokenSource();
        var token = _membershipRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                Interlocked.Increment(ref _catalogRefreshGeneration);
                InvalidateCache();
                if (refreshContinueWatching)
                {
                    await RefreshAllRowsAsync();
                }
                else if (batchItems is { Count: > 0 })
                {
                    var rows = GetRowsSnapshot()
                        .Where(r => !r.Config.ContinueWatching && RowMightBeAffectedByBatch(r, batchItems))
                        .ToList();
                    await Task.WhenAll(rows.Select(RefreshRowAsync));
                }
                else
                {
                    await RefreshNonContinueWatchingRowsAsync();
                }

                if (!token.IsCancellationRequested)
                    NotifyChanged();
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void ScheduleLayoutReload()
    {
        _membershipRefreshCts?.Cancel();
        _membershipRefreshCts?.Dispose();
        _membershipRefreshCts = new CancellationTokenSource();
        var token = _membershipRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                await ResetAndReloadAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    /// <summary>
    /// True when a loaded row is scoped to this library. Global rows (null/empty LibraryIds) do not
    /// count: they refresh for every library without representing a new library-specific feed.
    /// </summary>
    private bool HasRowTargetingLibrary(Guid libraryId) =>
        GetRowsSnapshot().Any(r => r.Config.LibraryIds is { Count: > 0 } ids && ids.Contains(libraryId));

    private bool RowMightBeAffectedByLibrary(Guid libraryId) =>
        GetRowsSnapshot().Any(r => r.Config.LibraryIds is null or { Count: 0 }
            || r.Config.LibraryIds.Contains(libraryId));

    private static bool RowMightBeAffectedByBatch(HomeFeedRow row, IReadOnlyList<MediaBatchItem> items)
    {
        var libraryIds = row.Config.LibraryIds is { Count: > 0 } ids ? ids.ToArray() : null;
        var mediaTypes = row.Config.MediaTypes is { Count: > 0 } types ? types : null;
        return MediaBrowseCarouselRefreshScope.IsBatchAffected(
            libraryIds,
            libraryGroupIds: null,
            mediaTypes,
            items);
    }

    private async Task<bool> RefreshContinueWatchingRowsAsync()
    {
        var rows = GetRowsSnapshot().Where(r => r.Config.ContinueWatching).ToList();
        if (rows.Count == 0)
            return true;

        var results = await Task.WhenAll(rows.Select(RefreshRowAsync));
        return results.All(ok => ok);
    }

    private async Task RefreshAllRowsAsync()
    {
        await Task.WhenAll(GetRowsSnapshot().Select(async row => await RefreshRowAsync(row)));
    }

    private async Task RefreshNonContinueWatchingRowsAsync()
    {
        var rows = GetRowsSnapshot().Where(r => !r.Config.ContinueWatching).ToList();
        await Task.WhenAll(rows.Select(RefreshRowAsync));
    }

    private async Task<bool> RefreshRowAsync(HomeFeedRow row)
    {
        var query = BuildQuery(row.Config);
        var items = await FetchRowAsync(query);
        if (items is null)
            return false;

        var cacheKey = BuildCacheKey(row.Config);
        _cacheStore.Set(cacheKey, items);

        lock (_sync)
        {
            ApplyRowItems(row.Items, items);
        }

        return true;
    }

    private async Task LoadRowAsync(HomeRowConfigDto config, List<MediaCardViewModel> target, CancellationToken cancellationToken)
    {
        var query = BuildQuery(config);
        var cacheKey = BuildCacheKey(config);
        var cached = _cacheStore.Get<List<MediaCardViewModel>>(cacheKey);

        if (cached is not null)
        {
            lock (_sync)
            {
                target.AddRange(cached);
            }

            _ = Task.Run(async () => await RefreshRowInBackground(query, cacheKey, target), cancellationToken);
            return;
        }

        var items = await FetchRowAsync(query);
        if (items is not null)
        {
            lock (_sync)
            {
                target.AddRange(items);
            }

            _cacheStore.Set(cacheKey, items);
        }
    }

    private async Task RefreshRowInBackground(GetHomeFeedQuery query, string cacheKey, List<MediaCardViewModel> target)
    {
        var generation = _catalogRefreshGeneration;
        var items = await FetchRowAsync(query);
        if (items is null || generation != _catalogRefreshGeneration)
            return;

        _cacheStore.Set(cacheKey, items);

        lock (_sync)
        {
            if (generation != _catalogRefreshGeneration)
                return;

            ApplyRowItems(target, items);
        }

        NotifyChanged();
    }

    /// <summary>
    /// Reuses existing card view-models by id so UI keys stay stable and images do not blink
    /// when only progress/watched changed. Replaces the instance when catalog visuals change
    /// (e.g. poster became available after MediaPicturesUpdated).
    /// </summary>
    private static void ApplyRowItems(List<MediaCardViewModel> target, List<MediaCardViewModel> items)
    {
        if (target.Count == items.Count
            && target.Zip(items, (existing, next) => existing.Id == next.Id).All(same => same))
        {
            for (var i = 0; i < target.Count; i++)
                target[i] = MergeCard(target[i], items[i]);

            return;
        }

        var existingById = target.ToDictionary(x => x.Id);
        var merged = new List<MediaCardViewModel>(items.Count);
        foreach (var item in items)
        {
            if (existingById.TryGetValue(item.Id, out var existing))
                merged.Add(MergeCard(existing, item));
            else
                merged.Add(item);
        }

        target.Clear();
        target.AddRange(merged);
    }

    private static MediaCardViewModel MergeCard(MediaCardViewModel existing, MediaCardViewModel next)
    {
        if (!HasCatalogVisualChanges(existing, next))
        {
            existing.Progress = next.Progress;
            existing.Watched = next.Watched;
            existing.GroupCount = next.GroupCount;
            return existing;
        }

        // Keep identical image URLs so K7Image does not remount for unchanged posters.
        var samePicture = MediaPictureUrlHelper.SameResourceUrl(existing.PictureUrl, next.PictureUrl);
        var sameBackdrop = MediaPictureUrlHelper.SameResourceUrl(existing.BackdropUrl, next.BackdropUrl);
        if (samePicture || sameBackdrop)
        {
            return next with
            {
                PictureUrl = samePicture ? existing.PictureUrl : next.PictureUrl,
                BackdropUrl = sameBackdrop ? existing.BackdropUrl : next.BackdropUrl
            };
        }

        return next;
    }

    private static bool HasCatalogVisualChanges(MediaCardViewModel existing, MediaCardViewModel next) =>
        !MediaPictureUrlHelper.SameResourceUrl(existing.PictureUrl, next.PictureUrl)
        || !MediaPictureUrlHelper.SameResourceUrl(existing.BackdropUrl, next.BackdropUrl)
        || existing.Title != next.Title
        || existing.AdditionalInformations != next.AdditionalInformations
        || existing.Overview != next.Overview
        || existing.TagLine != next.TagLine
        || existing.ContentRating != next.ContentRating
        || existing.RuntimeMinutes != next.RuntimeMinutes
        || existing.Rating != next.Rating
        || existing.ReleaseYear != next.ReleaseYear
        || existing.SerieSeasonCount != next.SerieSeasonCount
        || existing.SerieReleaseYear != next.SerieReleaseYear
        || existing.NavigationTarget != next.NavigationTarget
        || existing.SoftHeroBackdrop != next.SoftHeroBackdrop
        || !SameGenres(existing.Genres, next.Genres);

    private static bool SameGenres(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return left is null && right is null;

        return left.SequenceEqual(right);
    }

    private async Task<List<MediaCardViewModel>?> FetchRowAsync(GetHomeFeedQuery query)
    {
        try
        {
            return await ExecuteInScopeAsync(async sp =>
            {
                var mediaService = sp.GetRequiredService<IMediaService>();
                var apiClient = sp.GetRequiredService<IK7ServerService>();
                var feedPage = await mediaService.GetHomeFeedAsync(query);
                if (feedPage?.Items is null)
                    return null;

                return feedPage.Items.Select(item => item.ToCardViewModel(apiClient)).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Home feed row refresh failed (ContinueWatching={ContinueWatching})", query.ContinueWatching);
            return null;
        }
    }

    private async Task<HomeLayoutDto> LoadHomeLayoutWithRetryAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await ExecuteInScopeAsync(async sp =>
                    await sp.GetRequiredService<IUserPreferencesService>().GetHomeLayoutAsync(cancellationToken));
            }
            catch (HttpRequestException) when (_connectivity.IsOnline && attempt < maxAttempts - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400 * (attempt + 1)), cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && _connectivity.IsOnline && attempt < maxAttempts - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400 * (attempt + 1)), cancellationToken);
            }
        }

        return await ExecuteInScopeAsync(async sp =>
            await sp.GetRequiredService<IUserPreferencesService>().GetHomeLayoutAsync(cancellationToken));
    }

    private async Task<T> ExecuteInScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private GetHomeFeedQuery BuildQuery(HomeRowConfigDto config) => new()
    {
        ContinueWatching = config.ContinueWatching ? true : null,
        LibraryIds = config.LibraryIds?.ToArray(),
        MediaTypes = config.MediaTypes is { Count: > 0 } mt ? mt.ToHashSet() : null,
        // Continue Watching membership is ordered server-side; do not send LastInteractedDesc
        // (or any OrderBy) so InferStrategy cannot be confused by leftover layout options.
        OrderBy = config.ContinueWatching
            ? null
            : config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
        Detailed = _isTv,
        PageNumber = 1,
        PageSize = config.PageSize > 0 ? config.PageSize : 20
    };

    internal static string BuildFeedCacheKey(
        string? identityUserId,
        Guid? sharedProfileId,
        string title,
        bool continueWatching)
    {
        var userScope = string.IsNullOrEmpty(identityUserId) ? "anon" : identityUserId;
        var profileScope = sharedProfileId?.ToString("N") ?? "personal";
        return MediaCacheStore.BuildKey("home-feed", userScope, profileScope, title, continueWatching.ToString());
    }

    private string BuildCacheKey(HomeRowConfigDto config) =>
        BuildFeedCacheKey(
            _feedIdentityUserId,
            _sharedProfileSession.ActiveGroupId,
            config.Title,
            config.ContinueWatching);

    private static bool IsCardAffected(IReadOnlyList<MediaCardViewModel> items, Guid mediaId)
    {
        var id = mediaId.ToString();
        return items.Any(item => item.Id == id || item.ParentId == id);
    }

    private List<HomeFeedRow> GetRowsSnapshot()
    {
        lock (_sync)
        {
            return _rows.ToList();
        }
    }

    private void NotifyChanged() => Changed?.Invoke();
}
