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

    private bool isLoading { get; set; } = true;
    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _isAdmin;
    private List<(HomeRowConfigDto Config, List<MediaCardViewModel> Items)> _rows = [];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var role = await FeatureAccess.GetRoleAsync();
        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
        _canExclude = role is not null and not K7.Server.Domain.Constants.Roles.Guest;
        _isAdmin = role == K7.Server.Domain.Constants.Roles.Administrator;

        K7HubClient.MediaBatchAdded += OnMediaBatchAdded;

        HomeLayoutDto layout;
        try
        {
            layout = await PreferencesService.GetHomeLayoutAsync();
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

        isLoading = false;
        Shared.Services.AppReadySignal.Signal();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await SpatialNav.FocusFirstAsync("[data-carousel-item] a, [data-carousel-item] button");
            }
            catch (InvalidOperationException) { }
        }
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

        if (changed)
        {
            InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        K7HubClient.ProgressUpdated -= OnProgressUpdated;
        K7HubClient.MediaBatchAdded -= OnMediaBatchAdded;
    }

    private void OnMediaBatchAdded(List<MediaBatchItem> items)
    {
        CacheStore.InvalidateByPrefix("home-feed");

        _ = InvokeAsync(async () =>
        {
            await RefreshNonContinueWatchingRowsAsync();
            StateHasChanged();
        });
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
        var items = await FetchRowAsync(query);
        if (items is null) return;

        CacheStore.Set(cacheKey, items);

        await InvokeAsync(() =>
        {
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

    private string GetHref(MediaCardViewModel item) =>
        item.NavigationTarget ?? item.Kind switch
        {
            MediaCardKind.Cover => $"/music/albums/{item.ParentId ?? item.Id}",
            MediaCardKind.Serie => $"/series/{item.Id}",
            MediaCardKind.Season => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}",
            MediaCardKind.Episode => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}#ep-{item.EpisodeNumber}",
            _ => $"/movies/{item.Id}"
        };

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
}
