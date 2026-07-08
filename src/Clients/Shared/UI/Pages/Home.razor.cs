using System.Net.Http;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.UI.Pages;

public partial class Home : IDisposable
{
    [Inject] private IMediaService k7ServerService { get; set; } = default!;
    [Inject] private IK7ServerService apiClient { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private MediaCacheStore CacheStore { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IConnectivityService ConnectivityService { get; set; } = default!;

    private bool isLoading { get; set; } = true;
    private bool _isOffline;
    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _isAdmin;
    private bool _isTv;
    private MediaCardViewModel? _focusedItem;
    private List<(HomeRowConfigDto Config, List<MediaCardViewModel> Items)> _rows = [];
    private int _catalogRefreshGeneration;
    private DebouncedActionRunner? _picturesRefreshRunner;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var role = await FeatureAccess.GetRoleAsync();
        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
        _canExclude = role is not null and not K7.Server.Domain.Constants.Roles.Guest;
        _canSetWatchState = role is K7.Server.Domain.Constants.Roles.User or K7.Server.Domain.Constants.Roles.Administrator;
        _isAdmin = role == K7.Server.Domain.Constants.Roles.Administrator;
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;

        K7HubClient.MediaBatchAdded += OnMediaBatchAdded;
        K7HubClient.MediaIndexedFilesUpdated += OnMediaIndexedFilesUpdated;
        K7HubClient.LibraryScanCompleted += OnLibraryScanCompleted;
        K7HubClient.MediaMetadataRefreshed += OnMediaMetadataRefreshed;
        K7HubClient.MediaPicturesUpdated += OnMediaPicturesUpdated;
        CacheStore.HomeFeedInvalidated += OnHomeFeedInvalidated;

        _picturesRefreshRunner = new DebouncedActionRunner(RefreshAfterPicturesUpdatedAsync, InvokeAsync);

        HomeLayoutDto layout;
        try
        {
            layout = await PreferencesService.GetHomeLayoutAsync();
        }
        catch (HttpRequestException)
        {
            _isOffline = true;
            isLoading = false;
            return;
        }
        catch (TaskCanceledException)
        {
            _isOffline = true;
            isLoading = false;
            return;
        }
        catch
        {
            layout = new HomeLayoutDto { Rows = [] };
        }

        _rows = layout.Rows
            .Where(r => r.IsVisible)
            .OrderBy(r => r.Order)
            .Select(r => (r, new List<MediaCardViewModel>()))
            .ToList();

        if (_canTrackProgress)
            K7HubClient.ProgressUpdated += OnProgressUpdated;

        var tasks = _rows
            .Where(r => !r.Config.ContinueWatching || _canTrackProgress)
            .Select(r => LoadRowAsync(r.Config, r.Items))
            .ToList();

        await Task.WhenAll(tasks);

        if (_isTv)
        {
            _focusedItem = _rows
                .Where(r => r.Items.Count > 0)
                .Select(r => r.Items.First())
                .FirstOrDefault();
        }

        isLoading = false;
        Shared.Services.AppReadySignal.Signal();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !_isTv)
            return;

        try
        {
            await SpatialNav.FocusFirstAsync("[data-carousel-item] a, [data-carousel-item] button");
        }
        catch (InvalidOperationException) { }
    }

    private void OnProgressUpdated(Guid mediaId, double progressPercentage, bool isCompleted)
    {
        var id = mediaId.ToString();
        var changed = false;

        foreach (var (_, items) in _rows)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id)
                {
                    items[i] = items[i] with { Progress = progressPercentage, Watched = isCompleted };
                    changed = true;
                }
            }
        }

        if (changed && !isCompleted)
        {
            _ = InvokeAsync(StateHasChanged);
            return;
        }

        if (!_rows.Any(r => r.Config.ContinueWatching))
        {
            if (changed)
                _ = InvokeAsync(StateHasChanged);
            return;
        }

        CacheStore.InvalidateByPrefix("home-feed");
        _ = InvokeAsync(async () =>
        {
            await RefreshContinueWatchingRowsAsync();
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        K7HubClient.ProgressUpdated -= OnProgressUpdated;
        K7HubClient.MediaBatchAdded -= OnMediaBatchAdded;
        K7HubClient.MediaIndexedFilesUpdated -= OnMediaIndexedFilesUpdated;
        K7HubClient.LibraryScanCompleted -= OnLibraryScanCompleted;
        K7HubClient.MediaMetadataRefreshed -= OnMediaMetadataRefreshed;
        K7HubClient.MediaPicturesUpdated -= OnMediaPicturesUpdated;
        CacheStore.HomeFeedInvalidated -= OnHomeFeedInvalidated;
        _picturesRefreshRunner?.Dispose();
    }

    private void OnMediaMetadataRefreshed(Guid mediaId) =>
        ScheduleCatalogRefreshIfAffected(mediaId);

    private void OnMediaPicturesUpdated(Guid mediaId) =>
        ScheduleCatalogRefreshIfAffected(mediaId);

    private void ScheduleCatalogRefreshIfAffected(Guid mediaId)
    {
        if (isLoading || _isOffline)
            return;

        if (!_rows.Any(r => CatalogMediaRefreshMatcher.IsCardAffected(r.Items, mediaId)))
            return;

        _picturesRefreshRunner?.Schedule();
    }

    private async Task RefreshAfterPicturesUpdatedAsync()
    {
        Interlocked.Increment(ref _catalogRefreshGeneration);
        CacheStore.InvalidateByPrefix("home-feed");
        await RefreshAllRowsAsync();
        StateHasChanged();
    }

    private void OnHomeFeedInvalidated()
    {
        if (isLoading || _isOffline)
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);

        _ = InvokeAsync(async () =>
        {
            await RefreshAllRowsAsync();
            StateHasChanged();
        });
    }

    private void OnMediaBatchAdded(List<MediaBatchItem> items)
    {
        Interlocked.Increment(ref _catalogRefreshGeneration);
        CacheStore.InvalidateByPrefix("home-feed");

        _ = InvokeAsync(async () =>
        {
            await RefreshNonContinueWatchingRowsAsync();
            StateHasChanged();
        });
    }

    private void OnMediaIndexedFilesUpdated(Guid mediaId, Guid libraryId)
    {
        if (!RowMightBeAffectedByLibrary(libraryId))
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);
        CacheStore.InvalidateByPrefix("home-feed");

        _ = InvokeAsync(async () =>
        {
            await RefreshAllRowsAsync();
            StateHasChanged();
        });
    }

    private void OnLibraryScanCompleted(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount)
    {
        if (!RowMightBeAffectedByLibrary(libraryId))
            return;

        Interlocked.Increment(ref _catalogRefreshGeneration);
        CacheStore.InvalidateByPrefix("home-feed");

        _ = InvokeAsync(async () =>
        {
            await RefreshNonContinueWatchingRowsAsync();
            StateHasChanged();
        });
    }

    private bool RowMightBeAffectedByLibrary(Guid libraryId) =>
        _rows.Any(r => r.Config.LibraryIds is null or { Count: 0 }
            || r.Config.LibraryIds.Contains(libraryId));

    private async Task RefreshContinueWatchingRowsAsync()
    {
        var tasks = _rows
            .Where(r => r.Config.ContinueWatching)
            .Select(async r =>
            {
                var query = new GetHomeFeedQuery
                {
                    ContinueWatching = true,
                    LibraryIds = r.Config.LibraryIds?.ToArray(),
                    MediaTypes = r.Config.MediaTypes is { Count: > 0 } mt ? mt.ToHashSet() : null,
                    OrderBy = r.Config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
                    Detailed = _isTv,
                    PageNumber = 1,
                    PageSize = r.Config.PageSize
                };

                var items = await FetchRowAsync(query);
                if (items is not null)
                {
                    var cacheKey = MediaCacheStore.BuildKey("home-feed", r.Config.Title, r.Config.ContinueWatching.ToString());
                    CacheStore.Set(cacheKey, items);
                    r.Items.Clear();
                    r.Items.AddRange(items);
                }
            });

        await Task.WhenAll(tasks);
    }

    private async Task RefreshAllRowsAsync()
    {
        var tasks = _rows.Select(async r =>
        {
            var query = new GetHomeFeedQuery
            {
                ContinueWatching = r.Config.ContinueWatching ? true : null,
                LibraryIds = r.Config.LibraryIds?.ToArray(),
                MediaTypes = r.Config.MediaTypes is { Count: > 0 } mt ? mt.ToHashSet() : null,
                OrderBy = r.Config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
                Detailed = _isTv,
                PageNumber = 1,
                PageSize = r.Config.PageSize
            };

            var items = await FetchRowAsync(query);
            if (items is not null)
            {
                var cacheKey = MediaCacheStore.BuildKey("home-feed", r.Config.Title, r.Config.ContinueWatching.ToString());
                CacheStore.Set(cacheKey, items);
                r.Items.Clear();
                r.Items.AddRange(items);
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task RefreshNonContinueWatchingRowsAsync()
    {
        var tasks = _rows
            .Where(r => !r.Config.ContinueWatching)
            .Select(async r =>
            {
                var query = new GetHomeFeedQuery
                {
                    ContinueWatching = null,
                    LibraryIds = r.Config.LibraryIds?.ToArray(),
                    MediaTypes = r.Config.MediaTypes is { Count: > 0 } mt ? mt.ToHashSet() : null,
                    OrderBy = r.Config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
                    Detailed = _isTv,
                    PageNumber = 1,
                    PageSize = r.Config.PageSize
                };

                var items = await FetchRowAsync(query);
                if (items is not null)
                {
                    var cacheKey = MediaCacheStore.BuildKey("home-feed", r.Config.Title, r.Config.ContinueWatching.ToString());
                    CacheStore.Set(cacheKey, items);
                    r.Items.Clear();
                    r.Items.AddRange(items);
                }
            });

        await Task.WhenAll(tasks);
    }

    private async Task LoadRowAsync(HomeRowConfigDto config, List<MediaCardViewModel> target)
    {
        var query = new GetHomeFeedQuery
        {
            ContinueWatching = config.ContinueWatching ? true : null,
            LibraryIds = config.LibraryIds?.ToArray(),
            MediaTypes = config.MediaTypes is { Count: > 0 } mt ? mt.ToHashSet() : null,
            OrderBy = config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
            Detailed = _isTv,
            PageNumber = 1,
            PageSize = config.PageSize
        };

        var cacheKey = MediaCacheStore.BuildKey("home-feed", config.Title, config.ContinueWatching.ToString());
        var cached = CacheStore.Get<List<MediaCardViewModel>>(cacheKey);

        if (cached is not null)
        {
            target.AddRange(cached);
            _ = Task.Run(async () => await RefreshRowInBackground(query, cacheKey, target));
            return;
        }

        var items = await FetchRowAsync(query);
        if (items is not null)
        {
            target.AddRange(items);
            CacheStore.Set(cacheKey, items);
        }
    }

    private async Task RefreshRowInBackground(GetHomeFeedQuery query, string cacheKey, List<MediaCardViewModel> target)
    {
        var generation = _catalogRefreshGeneration;
        var items = await FetchRowAsync(query);
        if (items is null || generation != _catalogRefreshGeneration)
            return;

        CacheStore.Set(cacheKey, items);

        await InvokeAsync(() =>
        {
            if (generation != _catalogRefreshGeneration)
                return;

            target.Clear();
            target.AddRange(items);
            StateHasChanged();
        });
    }

    private async Task<List<MediaCardViewModel>?> FetchRowAsync(GetHomeFeedQuery query)
    {
        try
        {
            var feedPage = await k7ServerService.GetHomeFeedAsync(query);
            if (feedPage?.Items is null) return null;

            return feedPage.Items.Select(item => item.ToCardViewModel(apiClient)).ToList();
        }
        catch
        {
            return null;
        }
    }

    private string GetHref(MediaCardViewModel item)
    {
        if (!_isTv && item.Kind == MediaCardKind.Episode && TryGetEpisodePageHref(item, out var episodeHref))
            return episodeHref;

        return item.NavigationTarget ?? item.Kind switch
        {
            MediaCardKind.Cover => $"/music/albums/{item.ParentId ?? item.Id}",
            MediaCardKind.Serie => $"/series/{item.Id}",
            MediaCardKind.Season => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}",
            MediaCardKind.Episode => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}#ep-{item.EpisodeNumber}",
            _ => $"/movies/{item.Id}"
        };
    }

    private static bool TryGetEpisodePageHref(MediaCardViewModel item, out string href)
    {
        href = "";

        if (item.SeasonNumber is int season && item.EpisodeNumber is int episode)
        {
            var serieId = item.ParentId ?? item.Id;
            href = $"/series/{serieId}/seasons/{season}/episodes/{episode}";
            return true;
        }

        if (item.NavigationTarget is not { } nav)
            return false;

        const string anchor = "#ep-";
        var anchorIndex = nav.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorIndex <= 0 || !int.TryParse(nav.AsSpan(anchorIndex + anchor.Length), out var episodeNumber))
            return false;

        href = $"{nav[..anchorIndex]}/episodes/{episodeNumber}";
        return true;
    }

    private MediaCardVariant GetVariant(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Cover => MediaCardVariant.Cover,
        MediaCardKind.Episode => MediaCardVariant.Poster,
        _ => MediaCardVariant.Poster
    };

    private async Task ExcludeForSelf(MediaCardViewModel model)
    {
        try
        {
            var excluded = await UserAdminService.ToggleMediaExclusionAsync(Guid.Parse(model.Id));
            Snackbar.Add(excluded ? string.Format(S["Hidden"], model.Title) : string.Format(S["Unhidden"], model.Title), K7Severity.Success);

            if (excluded)
            {
                foreach (var (_, items) in _rows)
                    items.RemoveAll(x => x.Id == model.Id || x.ParentId == model.Id);
            }

            CacheStore.InvalidateByPrefix("home-feed");
            await RefreshAllRowsAsync();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task ExcludeForOthers(MediaCardViewModel model)
    {
        var parameters = new K7DialogParameters<ExcludeMediaForUsersDialog>
        {
            { x => x.MediaId, Guid.Parse(model.Id) },
            { x => x.MediaTitle, model.Title }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ExcludeMediaForUsersDialog>(S["HideForUser"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(S["ExclusionsUpdated"], K7Severity.Success);
        }
    }

    private void OnItemFocused(MediaCardViewModel item)
    {
        if (!_isTv) return;
        _focusedItem = item;
        StateHasChanged();
    }

    private string GetRowTitle(string rowTitle) => HomeLayoutRowTitleHelper.Localize(L, rowTitle);
}
