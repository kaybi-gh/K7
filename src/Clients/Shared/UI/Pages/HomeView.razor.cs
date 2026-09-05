using System.Net.Http;
using System.Security.Claims;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly SuppressRenderEventHandler _silentFocus = new();
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _isAdmin;
    private bool _hasConfiguredLibraries = true;
    private bool? _isTv;
    private MediaCardViewModel? _focusedItem;
    private bool _focusRestored;
    private bool _emptyFeedRetried;
    private bool _homeRestoreLoadFailed;
    private HomeTvHero? _tvHero;
    private IJSObjectReference? _homeRestoreModule;
    private bool _hubHomeActive;

    private bool _canTrackProgress;
    private string? _identityUserId;

    private bool isLoading => FeedStore.IsLoading;

    private bool _isOffline => FeedStore.IsOffline;

    private IReadOnlyList<HomeFeedRow> _rows => FeedStore.Rows;

    private string? _homeFocusSelector
    {
        get
        {
            if (_isTv != true)
                return null;

            if (NavigationState.SavedFocus is { } focus)
                return $"#home-card-{focus.MediaId} a, #home-card-{focus.MediaId} button";

            return "[data-carousel-item] a, [data-carousel-item] button";
        }
    }

    private int _tvInitialRowIndex
    {
        get
        {
            if (NavigationState.SavedFocus is not { } saved)
                return 0;

            var idx = GetVisibleRows().ToList().FindIndex(r => r.Config.Id == saved.RowId);
            return idx >= 0 ? idx : 0;
        }
    }

    protected override void OnInitialized()
    {
        // MAUI resolves device type synchronously; use it before any await so the first paint is TV.
        if (DeviceService.CachedDeviceType is { } cached)
            _isTv = cached == DeviceType.TV;
    }

    protected override async Task OnInitializedAsync()
    {
        _isTv ??= await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;

        FeedStore.Changed += OnFeedStoreChanged;
        FeedHub.Changed += OnFeedHubChanged;
        AuthStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        _hubHomeActive = IsHubHomeActive();
        // Resolve auth in the Blazor UI scope - HomeFeedStore must not open a fresh DI scope
        // (WASM DeserializedAuthenticationStateProvider is single-consume).
        await BindIdentityAndLoadAsync();

        if (_isAdmin)
        {
            try
            {
                var libraries = await LibraryService.GetLibrariesAsync();
                _hasConfiguredLibraries = libraries.Count > 0;
            }
            catch (HttpRequestException)
            {
                _hasConfiguredLibraries = true;
            }
            catch (InvalidOperationException)
            {
                _hasConfiguredLibraries = true;
            }
        }

        if (_isTv == true)
        {
            if (NavigationState.SavedFocus is { } saved && ResolveSavedFocus(saved) is { } resolved)
                _focusedItem = resolved.Item;
            else
                _focusedItem = GetVisibleRows().Select(r => r.Items.FirstOrDefault()).FirstOrDefault(i => i is not null);
        }
    }

    private async Task BindIdentityAndLoadAsync(Task<AuthenticationState>? authTask = null)
    {
        await BindIdentityAndCapabilitiesAsync(authTask);
        await FeedStore.EnsureLoadedAsync(_canTrackProgress, _identityUserId);
    }

    private async Task BindIdentityAndCapabilitiesAsync(Task<AuthenticationState>? authTask = null)
    {
        var state = await (authTask ?? AuthStateProvider.GetAuthenticationStateAsync());
        var userId = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? state.User.FindFirst("sub")?.Value;

        if (!string.Equals(_identityUserId, userId, StringComparison.Ordinal))
        {
            _identityUserId = userId;
            _emptyFeedRetried = false;
            _focusRestored = false;
            _focusedItem = null;
            NavigationState.Clear();
        }

        var role = await FeatureAccess.GetRoleAsync();
        _canExclude = role is not null and not K7.Server.Domain.Constants.Roles.Guest;
        _canSetWatchState = role is K7.Server.Domain.Constants.Roles.User or K7.Server.Domain.Constants.Roles.Administrator;
        _isAdmin = role == K7.Server.Domain.Constants.Roles.Administrator;
        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task) =>
        OnAuthenticationStateChangedAsync(task).FireAndForget();

    private async Task OnAuthenticationStateChangedAsync(Task<AuthenticationState> task)
    {
        try
        {
            await BindIdentityAndLoadAsync(task);
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        if (IsHubHomeActive())
            await InvokeAsync(StateHasChanged);
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
        // FeedHub parks Home while watching on Movie/Serie or while picking a profile.
        // Re-bind identity so a user switch reloads the store; otherwise only CW membership
        // can lag after playback.
        try
        {
            await BindIdentityAndLoadAsync();
            await FeedStore.RefreshContinueWatchingAsync();
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        // Wait for FeedHub to drop inert / paint the active page before focusing.
        await Task.Yield();
        await Task.Delay(50);

        // First launch: page data-initial-focus already ran. Only restore when
        // coming back from another page with a saved card.
        if (NavigationState.SavedFocus is not null)
            await RestoreLastFocusedCardAsync();
    }

    private async Task TryFocusHomeCarouselAsync()
    {
        if (_isTv != true)
            return;

        try
        {
            var onAppNav = await JSRuntime.InvokeAsync<bool>("K7.isAppNavFocused");
            if (onAppNav)
                return;

            await SpatialNav.FocusFirstAsync("[data-carousel-item] a, [data-carousel-item] button");
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task RestoreLastFocusedCardAsync()
    {
        if (NavigationState.SavedFocus is not { } saved || ResolveSavedFocus(saved) is not { } resolved)
        {
            await TryFocusHomeCarouselAsync();
            return;
        }

        if (_isTv == true)
        {
            _focusedItem = resolved.Item;
            await InvokeAsync(StateHasChanged);

            await EnsureHomeRestoreModuleAsync();
            if (_homeRestoreModule is not null)
            {
                try
                {
                    await _homeRestoreModule.InvokeAsync<bool>("scrollToCard", resolved.MediaId);
                }
                catch (JSException)
                {
                }
                catch (JSDisconnectedException)
                {
                }
            }
        }

        try
        {
            var focused = await JSRuntime.InvokeAsync<bool>("K7.focusById", GetHomeCardId(resolved.MediaId), true);
            if (!focused && _isTv == true)
                await TryFocusHomeCarouselAsync();
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task EnsureHomeRestoreModuleAsync()
    {
        if (_homeRestoreModule is not null || _homeRestoreLoadFailed)
            return;

        try
        {
            _homeRestoreModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/home-restore.js");
        }
        catch (JSException)
        {
            _homeRestoreLoadFailed = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (FeedStore.IsLoaded && !isLoading && !_isOffline && !_emptyFeedRetried && !GetVisibleRows().Any())
        {
            _emptyFeedRetried = true;
            await FeedStore.ResetAndReloadAsync();

            if (_isTv == true)
            {
                _focusedItem = GetVisibleRows().Select(r => r.Items.FirstOrDefault()).FirstOrDefault(i => i is not null);
            }

            return;
        }

        if (isLoading || _isOffline || _focusRestored)
            return;

        // Non-TV: FeedHub keep-alive preserves DOM (page + Embla). Do not call restore JS.
        if (_isTv != true)
        {
            _focusRestored = true;
            return;
        }

        if (_homeRestoreModule is null && !_homeRestoreLoadFailed)
        {
            await EnsureHomeRestoreModuleAsync();
            if (_homeRestoreLoadFailed)
            {
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

        _focusRestored = true;
    }

    private void OnFeedStoreChanged()
    {
        // While Home is parked (or another hub page is showing), skip re-renders.
        // OnHubHomeBecameActiveAsync refreshes Continue Watching and paints on return.
        if (FeedHub.IsEnabled && !FeedHub.IsHubRouteActive)
            return;

        if (FeedHub.IsEnabled && FeedHub.ActiveKey is { } active && active != FeedHubKey.Home)
            return;

        if (MauiNativeVideoChrome.BackgroundUiPaused)
            return;

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
        if (_isTv != true && item.Kind == MediaCardKind.Episode && TryGetEpisodePageHref(item, out var episodeHref))
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

    private static string GetCarouselContentKey(IReadOnlyList<MediaCardViewModel> items) =>
        string.Join(',', items.Select(i => i.Id));


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

    private EventCallback CreateSilentCardFocusCallback(HomeRowConfigDto row, MediaCardViewModel item, int cardIndex) =>
        EventCallback.Factory.Create(_silentFocus, () => OnItemFocused(row, item, cardIndex));

    private void OnItemFocused(HomeRowConfigDto row, MediaCardViewModel item, int cardIndex)
    {
        NavigationState.Save(row.Id, item.Id, cardIndex);

        if (_isTv != true)
            return;

        // Avoid re-render loops: focusin can re-fire after parent StateHasChanged patches the DOM.
        if (_focusedItem?.Id == item.Id)
            return;

        _focusedItem = item;
        _tvHero?.ApplyFocusedItem(item);
    }

    private string GetRowTitle(string rowTitle) => HomeLayoutRowTitleHelper.Localize(L, rowTitle);

    private static string GetHomeCardId(string mediaId) => $"home-card-{mediaId}";

    public async ValueTask DisposeAsync()
    {
        FeedStore.Changed -= OnFeedStoreChanged;
        FeedHub.Changed -= OnFeedHubChanged;
        AuthStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

        if (_homeRestoreModule is not null)
        {
            try
            {
                await _homeRestoreModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _homeRestoreModule = null;
        }
    }

    private sealed record ResolvedHomeFocus(MediaCardViewModel Item, int CardIndex)
    {
        public string MediaId => Item.Id;
    }
}
