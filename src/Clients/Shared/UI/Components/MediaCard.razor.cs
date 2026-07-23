using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.UI.Components;

public enum MediaCardVariant { Poster, Cover, Backdrop }

public partial class MediaCard : IDisposable
{
    [Parameter] public bool Skeleton { get; set; }
    [Parameter] public MediaCardViewModel Model { get; set; } = default!;
    [Parameter] public MediaCardVariant Variant { get; set; } = MediaCardVariant.Poster;
    [Parameter] public string? Href { get; set; }
    [Parameter] public bool OverlayEnabled { get; set; }
    [Parameter] public bool HoverOverlayEnabled { get; set; }
    [Parameter] public bool ContextMenuEnabled { get; set; } = true;
    [Parameter] public bool ProgressEnabled { get; set; }
    [Parameter] public bool WatchedStatusEnabled { get; set; }
    [Parameter] public bool FooterVisible { get; set; }
    [Parameter] public bool ExcludeMenuEnabled { get; set; }
    [Parameter] public bool ContinueWatchingMenuEnabled { get; set; }
    [Parameter] public bool WatchStateMenuEnabled { get; set; }
    [Parameter] public int? BulkEpisodeCount { get; set; }
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public EventCallback OnExcludeForSelf { get; set; }
    [Parameter] public EventCallback OnExcludeForOthers { get; set; }
    [Parameter] public EventCallback OnDismissFromContinueWatching { get; set; }
    [Parameter] public EventCallback OnWatchStateChanged { get; set; }
    [Parameter] public EventCallback OnFocused { get; set; }
    [Parameter] public string? ElementId { get; set; }
    [Parameter] public RenderFragment? CoverContent { get; set; }
    [Parameter] public string? PlaceholderIcon { get; set; }

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<MediaCard> Logger { get; set; } = default!;
    [Inject] private IMediaCardContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    private const int LongPressDelayMs = 600;
    private const double LongPressMoveThresholdSquared = 100;
    private const double DragCancelClickThresholdSquared = 100; // 10px

    private readonly Guid _menuOwnerId = Guid.NewGuid();
    private bool _menuOpen;
    private bool _longPressTriggered;
    private bool _keyHeldDown;
    private bool _menuOpenedViaKeyboard;
    private bool _preventNextClick;
    private bool _dragSuppressClick;
    private bool _watchStateMenuVisible;
    private bool _showRating;
    private bool _showReview;
    private bool _showPlaylist;
    private bool _showCollection;
    private string? _menuCapabilitiesKey;
    private CancellationTokenSource? _longPressCts;
    private double _touchStartX;
    private double _touchStartY;
    private double _pointerStartX;
    private double _pointerStartY;
    private ElementReference _longPressContainerRef;
    private DotNetObjectReference<MediaCard>? _longPressDotNetRef;
    private bool _longPressRegistered;

    private bool LongPressEnabled =>
        ContextMenuEnabled
        && (OverlayEnabled || ExcludeMenuEnabled || ContinueWatchingMenuEnabled || _watchStateMenuVisible || _showRating || _showReview || _showPlaylist || _showCollection);

    private string GetRootClass()
    {
        var classes = new List<string>();
        if (_menuOpen)
            classes.Add("media-card--menu-open");
        if (OverlayEnabled && !ExcludeMenuEnabled)
            classes.Add("media-card--play-only");
        return string.Join(" ", classes);
    }

    private string VariantClass => Variant switch
    {
        MediaCardVariant.Cover => "media-card--cover",
        MediaCardVariant.Backdrop => "media-card--backdrop",
        _ => "media-card--poster"
    };

    private bool ProgressBarIsHidden() => Model.Progress < 1 || Model.Progress >= 100;

    protected override void OnInitialized() =>
        ContextMenuService.Changed += OnContextMenuServiceChanged;

    protected override async Task OnParametersSetAsync()
    {
        if (Model is null)
        {
            _watchStateMenuVisible = false;
            _showRating = false;
            _showReview = false;
            _showPlaylist = false;
            _showCollection = false;
            _menuCapabilitiesKey = null;
            return;
        }

        var hasValidMediaId = Guid.TryParse(Model.Id, out _);
        var capabilitiesKey = $"{Model.Id}|{Model.Kind}|{Model.MediaType}|{WatchStateMenuEnabled}";
        if (_menuCapabilitiesKey == capabilitiesKey)
            return;

        _menuCapabilitiesKey = capabilitiesKey;

        _watchStateMenuVisible = hasValidMediaId
            && WatchStateMenuEnabled
            && WatchStateActions.SupportsWatchState(Model.Kind)
            && await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);

        var canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
        var canCreateLibrary = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
        var mediaType = MediaCardMenuActions.InferMediaType(Model);

        _showRating = hasValidMediaId && canRate;
        _showReview = hasValidMediaId && canRate && MediaCardMenuActions.SupportsReview(mediaType);
        _showPlaylist = hasValidMediaId && canCreateLibrary && MediaCardMenuActions.SupportsPlaylist(mediaType);
        _showCollection = hasValidMediaId && canCreateLibrary && MediaCardMenuActions.SupportsCollection(mediaType);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_longPressRegistered || !LongPressEnabled)
            return;

        try
        {
            _longPressDotNetRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("K7.registerMediaCardLongPress", _longPressContainerRef, _longPressDotNetRef);
            _longPressRegistered = true;
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSException)
        {
        }
    }

    private void OnContextMenuServiceChanged()
    {
        var open = ContextMenuService.Current?.OwnerId == _menuOwnerId;
        if (open == _menuOpen)
            return;

        _menuOpen = open;
        if (!open)
        {
            _longPressTriggered = false;
            _preventNextClick = false;
            _menuOpenedViaKeyboard = false;
            CancelLongPress();
        }

        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OpenContextMenuFromLongPressAsync()
    {
        if (!LongPressEnabled)
            return;

        _longPressTriggered = true;
        _preventNextClick = true;
        _menuOpenedViaKeyboard = true;
        _keyHeldDown = false;
        CancelLongPress();

        try
        {
            await JS.InvokeVoidAsync("K7.suppressEnterUntilKeyUp");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSException)
        {
        }

        await OpenSharedMenuAsync();
    }

    [JSInvokable]
    public Task CloseContextMenuFromBackAsync()
    {
        if (!_menuOpen)
            return Task.CompletedTask;

        ContextMenuService.Close();
        return Task.CompletedTask;
    }

    private Task OpenSharedMenuAsync()
    {
        if (!LongPressEnabled || Model is null)
            return Task.CompletedTask;

        _longPressTriggered = true;
        _preventNextClick = true;
        CancelLongPress();

        if (_menuOpenedViaKeyboard)
        {
            _menuOpenedViaKeyboard = false;
            try
            {
                _ = JS.InvokeVoidAsync("K7.suppressEnterUntilKeyUp");
            }
            catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException)
            {
            }
        }

        // Persist focus/scroll restore target even when activation goes through the menu.
        _ = NotifyFocusedAsync();

        ContextMenuService.Open(new MediaCardContextMenuRequest
        {
            OwnerId = _menuOwnerId,
            Model = Model,
            Anchor = _longPressContainerRef,
            AnchorKind = MediaCardContextMenuAnchorKind.Card,
            Href = Href,
            Title = Model.Title,
            ShowPlay = OverlayEnabled && !string.IsNullOrEmpty(Href),
            ShowRating = _showRating,
            ShowReview = _showReview,
            ShowPlaylist = _showPlaylist,
            ShowCollection = _showCollection,
            ShowWatchState = _watchStateMenuVisible,
            ExcludeMenuEnabled = ExcludeMenuEnabled,
            ContinueWatchingMenuEnabled = ContinueWatchingMenuEnabled,
            IsAdmin = IsAdmin,
            BulkEpisodeCount = BulkEpisodeCount,
            OnExcludeForSelf = OnExcludeForSelf,
            OnExcludeForOthers = OnExcludeForOthers,
            OnDismissFromContinueWatching = OnDismissFromContinueWatching,
            OnWatchStateChanged = OnWatchStateChanged
        });

        return Task.CompletedTask;
    }

    private void OnContextMenu(MouseEventArgs e)
    {
        if (!LongPressEnabled)
            return;

        _longPressTriggered = true;
        _preventNextClick = true;
        OpenSharedMenuAsync().FireAndForget(Logger);
    }

    private static bool IsEnterKey(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "NumpadEnter" or "Select" or "DpadCenter")
            return true;

        var code = string.IsNullOrEmpty(e.Code) ? e.Key : e.Code;
        return code is "Enter" or "NumpadEnter" or "Select" or "DpadCenter";
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (!LongPressEnabled || !IsEnterKey(e))
            return;

        if (e.Repeat && _longPressCts is not null)
            return;

        _keyHeldDown = true;
        CancelLongPress();
        _longPressTriggered = false;
        _longPressCts = new CancellationTokenSource();
        WaitForLongPressAsync(_longPressCts.Token, fromKeyboard: true).FireAndForget(Logger);
    }

    private async Task OnKeyUp(KeyboardEventArgs e)
    {
        if (!LongPressEnabled || !IsEnterKey(e))
            return;

        CancelLongPress();

        var wasShortPress = _keyHeldDown && !_longPressTriggered;
        _keyHeldDown = false;

        if (_longPressTriggered)
        {
            _preventNextClick = true;
            return;
        }

        if (wasShortPress && !string.IsNullOrEmpty(Href))
        {
            await NotifyFocusedAsync();
            NavigationManager.NavigateTo(Href);
        }
    }

    private void OnTouchStart(TouchEventArgs e)
    {
        if (!LongPressEnabled || e.Touches.Length == 0)
            return;

        CancelLongPress();
        _longPressTriggered = false;
        _touchStartX = e.Touches[0].ClientX;
        _touchStartY = e.Touches[0].ClientY;
        _longPressCts = new CancellationTokenSource();
        WaitForLongPressAsync(_longPressCts.Token).FireAndForget(Logger);
    }

    private void OnTouchMove(TouchEventArgs e)
    {
        if (_longPressCts is null || e.Touches.Length == 0)
            return;

        var dx = e.Touches[0].ClientX - _touchStartX;
        var dy = e.Touches[0].ClientY - _touchStartY;
        if (dx * dx + dy * dy > LongPressMoveThresholdSquared)
            CancelLongPress();
    }

    private void OnTouchEnd(TouchEventArgs e)
    {
        if (_longPressTriggered)
            _preventNextClick = true;

        CancelLongPress();
    }

    private void OnTouchCancel(TouchEventArgs e) => CancelLongPress();

    private async Task WaitForLongPressAsync(CancellationToken cancellationToken, bool fromKeyboard = false)
    {
        try
        {
            await Task.Delay(LongPressDelayMs, cancellationToken);
            _longPressTriggered = true;
            _preventNextClick = true;

            if (fromKeyboard)
            {
                _menuOpenedViaKeyboard = true;

                // The physical keyup that ends this long-press is swallowed by
                // navigation.js (K7._suppressEnterUntilKeyUp) and never reaches
                // OnKeyUp, so this flag must be cleared here or it stays stuck
                // "true". Otherwise a later, unrelated keyup that lands back on
                // this element (e.g. after closing the context menu restores
                // focus here) gets misread as the release of a genuine short
                // press and triggers navigation.
                _keyHeldDown = false;

                try
                {
                    await JS.InvokeVoidAsync("K7.suppressEnterUntilKeyUp");
                }
                catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException)
                {
                }
            }

            await InvokeAsync(OpenSharedMenuAsync);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void CancelLongPress()
    {
        _longPressCts?.Cancel();
        _longPressCts?.Dispose();
        _longPressCts = null;
    }

    private bool ShouldPreventLinkActivation =>
        _preventNextClick || _keyHeldDown || _longPressTriggered || _menuOpen || _dragSuppressClick;

    private void OnLinkPointerDown(PointerEventArgs e)
    {
        if (e.Button != 0)
            return;

        _dragSuppressClick = false;
        _pointerStartX = e.ClientX;
        _pointerStartY = e.ClientY;
    }

    private void OnLinkPointerMove(PointerEventArgs e)
    {
        if (_dragSuppressClick)
            return;

        var dx = e.ClientX - _pointerStartX;
        var dy = e.ClientY - _pointerStartY;
        if (dx * dx + dy * dy <= DragCancelClickThresholdSquared)
            return;

        _dragSuppressClick = true;
        // Refresh @onclick:preventDefault before the click event is dispatched.
        StateHasChanged();
    }

    private async Task OnLinkClick(MouseEventArgs e)
    {
        var suppress = ShouldPreventLinkActivation;
        _dragSuppressClick = false;

        if (suppress)
        {
            // Keep _preventNextClick for long-press / menu flows; drag only needed this click.
            if (_longPressTriggered || _menuOpen || _keyHeldDown)
                _preventNextClick = true;
            return;
        }

        // Touch / mouse often activate the link without a prior focusin.
        // Notify parents before navigation so scroll/focus restore can save position.
        await NotifyFocusedAsync();
    }

    private Task NotifyFocusedAsync() =>
        OnFocused.HasDelegate ? OnFocused.InvokeAsync() : Task.CompletedTask;

    public void Dispose()
    {
        ContextMenuService.Changed -= OnContextMenuServiceChanged;
        if (_menuOpen)
            ContextMenuService.Close();

        CancelLongPress();

        if (_longPressRegistered)
        {
            try
            {
                JS.InvokeVoidAsync("K7.unregisterMediaCardLongPress", _longPressContainerRef)
                    .AsTask()
                    .FireAndForget(Logger);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException)
            {
            }
        }

        _longPressDotNetRef?.Dispose();
    }

    private string ResolvedPlaceholderIcon => PlaceholderIcon ?? Variant switch
    {
        MediaCardVariant.Cover => Phosphor.VinylRecord,
        _ => Phosphor.FilmSlate
    };

    private RenderFragment CoverVisual => builder =>
    {
        if (CoverContent is not null)
        {
            builder.AddContent(0, CoverContent);
            return;
        }

        if (!string.IsNullOrEmpty(Model.PictureUrl))
        {
            builder.OpenComponent<K7Image>(1);
            builder.AddAttribute(2, "Src", Model.PictureUrl);
            builder.AddAttribute(3, "Alt", Model.Title);
            builder.AddAttribute(4, "Class", "rounded-lg");
            builder.AddAttribute(5, "Fluid", true);
            builder.AddAttribute(6, "ObjectFit", "cover");
            builder.AddAttribute(7, "loading", "lazy");
            builder.AddAttribute(8, "LoadingMode", K7ImageLoadingMode.Css);
            builder.AddAttribute(9, "FallbackContent", CardPlaceholder);
            builder.CloseComponent();
            return;
        }

        builder.AddContent(9, CardPlaceholder);
    };
}
