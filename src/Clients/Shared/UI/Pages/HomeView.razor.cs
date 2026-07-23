using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class HomeView : IAsyncDisposable
{
    [Inject] private IMediaService k7ServerService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IHomeFeedStore FeedStore { get; set; } = default!;
    [Inject] private IHomeNavigationState NavigationState { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject] private IFeedHubHostService FeedHub { get; set; } = default!;

    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _isAdmin;
    private bool _isTv;
    private MediaCardViewModel? _focusedItem;
    private bool _focusRestored;
    private bool _emptyFeedRetried;
    private bool _homeRestoreLoadFailed;
    private IJSObjectReference? _homeRestoreModule;
    private bool _hubHomeActive;
    private bool _feedUpdatePending;

    private bool isLoading => FeedStore.IsLoading;

    private bool _isOffline => FeedStore.IsOffline;

    private bool _canTrackProgress => FeedStore.CanTrackProgress;

    private IReadOnlyList<HomeFeedRow> _rows => FeedStore.Rows;

    private string? _homeFocusSelector
    {
        get
        {
            if (!_isTv)
                return null;

            if (NavigationState.SavedFocus is { } focus)
                return $"#home-card-{focus.MediaId} a, #home-card-{focus.MediaId} button";

            return "[data-carousel-item] a, [data-carousel-item] button";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        var role = await FeatureAccess.GetRoleAsync();
        _canExclude = role is not null and not K7.Server.Domain.Constants.Roles.Guest;
        _canSetWatchState = role is K7.Server.Domain.Constants.Roles.User or K7.Server.Domain.Constants.Roles.Administrator;
        _isAdmin = role == K7.Server.Domain.Constants.Roles.Administrator;
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;

        FeedStore.Changed += OnFeedStoreChanged;
        FeedHub.Changed += OnFeedHubChanged;
        _hubHomeActive = IsHubHomeActive();
        await FeedStore.EnsureLoadedAsync();

        if (_isTv)
        {
            if (NavigationState.SavedFocus is { } saved && ResolveSavedFocus(saved) is { } resolved)
                _focusedItem = resolved.Item;
            else
                _focusedItem = GetVisibleRows().Select(r => r.Items.FirstOrDefault()).FirstOrDefault(i => i is not null);
        }
    }

    private bool IsHubHomeActive() =>
        !FeedHub.IsEnabled
        || (FeedHub.IsHubRouteActive && FeedHub.ActiveKey == FeedHubKey.Home);

    private void OnFeedHubChanged()
    {
        var homeActive = IsHubHomeActive();
        var becameActive = homeActive && !_hubHomeActive;
        _hubHomeActive = homeActive;

        if (!becameActive)
            return;

        InvokeAsync(OnHubHomeBecameActiveAsync).FireAndForget();
    }

    private async Task OnHubHomeBecameActiveAsync()
    {
        if (_feedUpdatePending)
        {
            _feedUpdatePending = false;
            StateHasChanged();
            await Task.Yield();
        }

        await RestoreLastFocusedCardAsync();
    }

    private async Task RestoreLastFocusedCardAsync()
    {
        if (NavigationState.SavedFocus is not { } saved || ResolveSavedFocus(saved) is not { } resolved)
            return;

        if (_isTv)
        {
            _focusedItem = resolved.Item;
            await InvokeAsync(StateHasChanged);
        }

        try
        {
            // preventScroll: keep Embla / page scroll exactly as parked by FeedHub.
            await JSRuntime.InvokeVoidAsync("K7.focusById", GetHomeCardId(resolved.MediaId), true);
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (FeedStore.IsLoaded && !isLoading && !_isOffline && !_emptyFeedRetried && !GetVisibleRows().Any())
        {
            _emptyFeedRetried = true;
            await FeedStore.ResetAndReloadAsync();

            if (_isTv)
            {
                _focusedItem = GetVisibleRows().Select(r => r.Items.FirstOrDefault()).FirstOrDefault(i => i is not null);
            }

            return;
        }

        if (isLoading || _isOffline || _focusRestored)
            return;

        // Non-TV: FeedHub keep-alive preserves DOM (page + Embla). Do not call restore JS.
        if (!_isTv)
        {
            _focusRestored = true;
            return;
        }

        if (_homeRestoreModule is null && !_homeRestoreLoadFailed)
        {
            try
            {
                _homeRestoreModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/K7.Clients.Shared.UI/js/home-restore.js");
            }
            catch (JSException)
            {
                _homeRestoreLoadFailed = true;
                _focusRestored = true;
                return;
            }
        }

        if (_homeRestoreModule is null)
            return;

        if (NavigationState.SavedFocus is { } savedFocus)
        {
            var resolvedFocus = ResolveSavedFocus(savedFocus);
            if (resolvedFocus is not null)
            {
                try
                {
                    await _homeRestoreModule.InvokeAsync<bool>("scrollToCard", resolvedFocus.MediaId);

                    try
                    {
                        await JSRuntime.InvokeVoidAsync("K7.focusById", $"home-card-{resolvedFocus.MediaId}", true);
                    }
                    catch (JSException)
                    {
                    }

                    _focusedItem = resolvedFocus.Item;
                }
                catch (JSException)
                {
                }
                finally
                {
                    _focusRestored = true;
                }

                return;
            }
        }

        try
        {
            await SpatialNav.FocusFirstAsync("[data-carousel-item] a, [data-carousel-item] button");
            _focusRestored = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnFeedStoreChanged()
    {
        // While Home is parked (or another hub page is showing), buffer store updates.
        // Flushing while hidden would rebuild carousels and reset Embla; applying on return
        // keeps scroll identical when nothing changed, and shows new media when something did.
        if (FeedHub.IsEnabled && !FeedHub.IsHubRouteActive)
        {
            _feedUpdatePending = true;
            return;
        }

        if (FeedHub.IsEnabled && FeedHub.ActiveKey is { } active && active != FeedHubKey.Home)
        {
            _feedUpdatePending = true;
            return;
        }

        InvokeAsync(StateHasChanged).FireAndForget();
    }

    private IEnumerable<HomeFeedRow> GetVisibleRows() =>
        _rows.Where(r => r.Items.Count > 0 && (!r.Config.ContinueWatching || _canTrackProgress));

    private ResolvedHomeFocus? ResolveSavedFocus(HomeFocusState saved)
    {
        var visibleRows = GetVisibleRows().ToList();
        var rowIndex = visibleRows.FindIndex(r => r.Config.Id == saved.RowId);
        if (rowIndex < 0)
            return null;

        var row = visibleRows[rowIndex];
        var itemIndex = row.Items.FindIndex(i => i.Id == saved.MediaId);
        if (itemIndex < 0)
            itemIndex = Math.Clamp(saved.CardIndex, 0, Math.Max(0, row.Items.Count - 1));

        if (itemIndex < 0 || itemIndex >= row.Items.Count)
            return null;

        return new ResolvedHomeFocus(row.Items[itemIndex], itemIndex);
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

    private async Task DismissFromContinueWatching(MediaCardViewModel model)
    {
        try
        {
            await k7ServerService.DismissFromContinueWatchingAsync(Guid.Parse(model.Id));
            Snackbar.Add(string.Format(L["RemovedFromContinueWatching"], model.Title), K7Severity.Success);
            FeedStore.RemoveMedia(model.Id);
            FeedStore.InvalidateCache();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task ExcludeForSelf(MediaCardViewModel model)
    {
        try
        {
            var excluded = await UserAdminService.ToggleMediaExclusionAsync(Guid.Parse(model.Id));
            Snackbar.Add(excluded ? string.Format(S["Hidden"], model.Title) : string.Format(S["Unhidden"], model.Title), K7Severity.Success);

            if (excluded)
                FeedStore.RemoveMediaAndChildren(model.Id);

            FeedStore.InvalidateCache();
            await FeedStore.RefreshAsync();
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
            Snackbar.Add(S["ExclusionsUpdated"], K7Severity.Success);
    }

    private void OnItemFocused(HomeRowConfigDto row, MediaCardViewModel item, int cardIndex)
    {
        NavigationState.Save(row.Id, item.Id, cardIndex);

        if (!_isTv)
            return;

        // Avoid re-render loops: focusin can re-fire after parent StateHasChanged patches the DOM.
        if (_focusedItem?.Id == item.Id)
            return;

        _focusedItem = item;
        StateHasChanged();
    }

    private string GetRowTitle(string rowTitle) => HomeLayoutRowTitleHelper.Localize(L, rowTitle);

    private static string GetHomeCardId(string mediaId) => $"home-card-{mediaId}";

    public async ValueTask DisposeAsync()
    {
        FeedStore.Changed -= OnFeedStoreChanged;
        FeedHub.Changed -= OnFeedHubChanged;

        if (_homeRestoreModule is not null)
        {
            try
            {
                await _homeRestoreModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private sealed record ResolvedHomeFocus(MediaCardViewModel Item, int CardIndex)
    {
        public string MediaId => Item.Id;
    }
}
