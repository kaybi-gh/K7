using System.Net.Http;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;

public sealed class HomeFeedStore : IHomeFeedStore, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly K7HubClient _hubClient;
    private readonly MediaCacheStore _cacheStore;
    private readonly IDeviceService _deviceService;
    private readonly IConnectivityService _connectivity;

    private readonly List<HomeFeedRow> _rows = [];
    private readonly object _sync = new();
    private CancellationTokenSource? _picturesRefreshCts;
    private CancellationTokenSource? _continueWatchingRefreshCts;
    private Task? _loadTask;
    private int _catalogRefreshGeneration;
    private bool _isLoaded;
    private bool _isTv;
    private bool _hubHandlersRegistered;

    private static readonly TimeSpan ContinueWatchingRefreshDelay = TimeSpan.FromSeconds(1.5);

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
        IConnectivityService connectivity)
    {
        _scopeFactory = scopeFactory;
        _hubClient = hubClient;
        _cacheStore = cacheStore;
        _deviceService = deviceService;
        _connectivity = connectivity;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public Task ResetAndReloadAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _isLoaded = false;
            _loadTask = null;
            _rows.Clear();
        }

        IsOffline = false;
        InvalidateCache();
        return EnsureLoadedAsync(cancellationToken);
    }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_isLoaded && _loadTask is { IsCompletedSuccessfully: true })
                return Task.CompletedTask;

            if (_loadTask is { IsFaulted: true } or { IsCanceled: true })
                _loadTask = null;

            _loadTask ??= LoadAsync(cancellationToken);
            return _loadTask;
        }
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

    public void Dispose()
    {
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        UnregisterHubHandlers();
        _picturesRefreshCts?.Cancel();
        _picturesRefreshCts?.Dispose();
        _continueWatchingRefreshCts?.Cancel();
        _continueWatchingRefreshCts?.Dispose();
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
            await EnsureLoadedAsync();
        }
        catch
        {
            // Best effort; UI will reflect store state on next Changed.
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        RegisterHubHandlers();

        IsLoading = true;
        IsOffline = false;
        NotifyChanged();

        CanTrackProgress = await ExecuteInScopeAsync(async sp =>
            await sp.GetRequiredService<IFeatureAccessService>().HasCapabilityAsync(Capability.CanResumePlayback));
        _isTv = await _deviceService.GetDeviceTypeAsync() == DeviceType.TV;

        HomeLayoutDto layout;
        try
        {
            layout = await LoadHomeLayoutWithRetryAsync(cancellationToken);
        }
        catch (HttpRequestException) when (!_connectivity.IsOnline)
        {
            IsOffline = true;
            IsLoading = false;
            _isLoaded = true;
            NotifyChanged();
            return;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && !_connectivity.IsOnline)
        {
            IsOffline = true;
            IsLoading = false;
            _isLoaded = true;
            NotifyChanged();
            return;
        }
        catch (HttpRequestException)
        {
            FailTransientLoad();
            return;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            FailTransientLoad();
            return;
        }
        catch
        {
            layout = new HomeLayoutDto { Rows = [] };
        }

        var rowConfigs = layout.Rows
            .Where(r => r.IsVisible)
            .OrderBy(r => r.Order)
            .ToList();

        lock (_sync)
        {
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

        IsLoading = false;
        _isLoaded = true;
        NotifyChanged();
        AppReadySignal.Signal();
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
            return;

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
                    if (row.Items[i].Id != id)
                        continue;

                    // Mutate in place so Blazor keeps the same card instance (@key) and posters do not blink.
                    row.Items[i].Progress = progressPercentage;
                    row.Items[i].Watched = isCompleted;
                    changed = true;
                }
            }
        }

        if (changed)
            NotifyChanged();

        // Membership may still change (enter/leave Keep Watching). Debounce so progress ticks
        // only patch bars; a quiet period triggers one soft membership sync.
        if (!GetRowsSnapshot().Any(r => r.Config.ContinueWatching))
            return;

        ScheduleContinueWatchingRefresh(isCompleted ? TimeSpan.Zero : ContinueWatchingRefreshDelay);
    }

    private void ScheduleContinueWatchingRefresh(TimeSpan delay)
    {
        _continueWatchingRefreshCts?.Cancel();
        _continueWatchingRefreshCts?.Dispose();
        _continueWatchingRefreshCts = new CancellationTokenSource();
        var token = _continueWatchingRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, token);

                InvalidateCache();
                await RefreshContinueWatchingRowsAsync();
                if (!token.IsCancellationRequested)
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
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);
        _ = RefreshAllRowsAsync().ContinueWith(_ => NotifyChanged(), TaskScheduler.Default);
    }

    private void OnMediaBatchAdded(List<MediaBatchItem> items)
    {
        if (!_isLoaded || IsLoading || IsOffline)
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);
        InvalidateCache();
        _ = RefreshNonContinueWatchingRowsAsync().ContinueWith(_ => NotifyChanged(), TaskScheduler.Default);
    }

    private void OnMediaIndexedFilesUpdated(Guid mediaId, Guid libraryId)
    {
        if (!_isLoaded || IsLoading || IsOffline || !RowMightBeAffectedByLibrary(libraryId))
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);
        InvalidateCache();
        _ = RefreshAllRowsAsync().ContinueWith(_ => NotifyChanged(), TaskScheduler.Default);
    }

    private void OnLibraryScanCompleted(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount)
    {
        if (!_isLoaded || IsLoading || IsOffline || !RowMightBeAffectedByLibrary(libraryId))
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);
        InvalidateCache();
        _ = RefreshNonContinueWatchingRowsAsync().ContinueWith(_ => NotifyChanged(), TaskScheduler.Default);
    }

    private bool RowMightBeAffectedByLibrary(Guid libraryId) =>
        GetRowsSnapshot().Any(r => r.Config.LibraryIds is null or { Count: 0 }
            || r.Config.LibraryIds.Contains(libraryId));

    private async Task RefreshContinueWatchingRowsAsync()
    {
        var rows = GetRowsSnapshot().Where(r => r.Config.ContinueWatching).ToList();
        await Task.WhenAll(rows.Select(RefreshRowAsync));
    }

    private async Task RefreshAllRowsAsync()
    {
        await Task.WhenAll(GetRowsSnapshot().Select(RefreshRowAsync));
    }

    private async Task RefreshNonContinueWatchingRowsAsync()
    {
        var rows = GetRowsSnapshot().Where(r => !r.Config.ContinueWatching).ToList();
        await Task.WhenAll(rows.Select(RefreshRowAsync));
    }

    private async Task RefreshRowAsync(HomeFeedRow row)
    {
        var query = BuildQuery(row.Config);
        var items = await FetchRowAsync(query);
        if (items is null)
            return;

        var cacheKey = BuildCacheKey(row.Config);
        _cacheStore.Set(cacheKey, items);

        lock (_sync)
        {
            ApplyRowItems(row.Items, items);
        }
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
    /// Reuses existing card view-models by id so UI keys stay stable and images do not reload.
    /// </summary>
    private static void ApplyRowItems(List<MediaCardViewModel> target, List<MediaCardViewModel> items)
    {
        if (target.Count == items.Count
            && target.Zip(items, (existing, next) => existing.Id == next.Id).All(same => same))
        {
            for (var i = 0; i < target.Count; i++)
            {
                target[i].Progress = items[i].Progress;
                target[i].Watched = items[i].Watched;
            }

            return;
        }

        var existingById = target.ToDictionary(x => x.Id);
        var merged = new List<MediaCardViewModel>(items.Count);
        foreach (var item in items)
        {
            if (existingById.TryGetValue(item.Id, out var existing))
            {
                existing.Progress = item.Progress;
                existing.Watched = item.Watched;
                merged.Add(existing);
            }
            else
            {
                merged.Add(item);
            }
        }

        target.Clear();
        target.AddRange(merged);
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
        catch
        {
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
        OrderBy = config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
        Detailed = _isTv,
        PageNumber = 1,
        PageSize = config.PageSize
    };

    private static string BuildCacheKey(HomeRowConfigDto config) =>
        MediaCacheStore.BuildKey("home-feed", config.Title, config.ContinueWatching.ToString());

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
