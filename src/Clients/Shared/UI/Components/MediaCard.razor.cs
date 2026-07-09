using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

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
    [Parameter] public RenderFragment? CoverContent { get; set; }
    [Parameter] public string? PlaceholderIcon { get; set; }

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private const int LongPressDelayMs = 600;
    private const double LongPressMoveThresholdSquared = 100;

    private bool _menuOpen;
    private bool _longPressTriggered;
    private bool _keyHeldDown;
    private bool _menuOpenedViaKeyboard;
    private bool _preventNextClick;
    private bool _watchStateMenuVisible;
    private bool _showRating;
    private bool _showReview;
    private bool _showPlaylist;
    private bool _showCollection;
    private CancellationTokenSource? _longPressCts;
    private double _touchStartX;
    private double _touchStartY;

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

    protected override async Task OnParametersSetAsync()
    {
        if (Model is null)
        {
            _watchStateMenuVisible = false;
            _showRating = false;
            _showReview = false;
            _showPlaylist = false;
            _showCollection = false;
            return;
        }

        var hasValidMediaId = Guid.TryParse(Model.Id, out _);

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

    private async void OnMenuOpenChanged(bool open)
    {
        _menuOpen = open;
        if (!open)
            return;

        _longPressTriggered = true;
        _preventNextClick = true;
        CancelLongPress();

        if (_menuOpenedViaKeyboard)
        {
            _menuOpenedViaKeyboard = false;
            try
            {
                await JS.InvokeVoidAsync("K7.suppressEnterUntilKeyUp");
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private void OnContextMenu(MouseEventArgs e)
    {
        if (!LongPressEnabled)
            return;

        _longPressTriggered = true;
        _preventNextClick = true;
        _menuOpen = true;
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (!LongPressEnabled || !IsEnterKey(e.Key))
            return;

        if (e.Repeat && _longPressCts is not null)
            return;

        _keyHeldDown = true;
        CancelLongPress();
        _longPressTriggered = false;
        _longPressCts = new CancellationTokenSource();
        _ = WaitForLongPressAsync(_longPressCts.Token, fromKeyboard: true);

        try
        {
            _ = JS.InvokeVoidAsync("K7.suppressEnterUntilKeyUp");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (!LongPressEnabled || !IsEnterKey(e.Key))
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
            NavigationManager.NavigateTo(Href);
    }

    private static bool IsEnterKey(string? key) =>
        key is "Enter" or "NumpadEnter";

    private void OnTouchStart(TouchEventArgs e)
    {
        if (!LongPressEnabled || e.Touches.Length == 0)
            return;

        CancelLongPress();
        _longPressTriggered = false;
        _touchStartX = e.Touches[0].ClientX;
        _touchStartY = e.Touches[0].ClientY;
        _longPressCts = new CancellationTokenSource();
        _ = WaitForLongPressAsync(_longPressCts.Token);
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
                try
                {
                    await JS.InvokeVoidAsync("K7.suppressEnterUntilKeyUp");
                }
                catch (JSDisconnectedException)
                {
                }
            }

            _menuOpen = true;
            await InvokeAsync(StateHasChanged);
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
        _preventNextClick || _keyHeldDown || _longPressTriggered || _menuOpen;

    private void OnLinkClick(MouseEventArgs e)
    {
        if (ShouldPreventLinkActivation)
            _preventNextClick = true;
    }

    public void Dispose()
    {
        CancelLongPress();
    }

    private string ResolvedPlaceholderIcon => PlaceholderIcon ?? Variant switch
    {
        MediaCardVariant.Cover => Phosphor.VinylRecord,
        _ => Phosphor.FilmSlate
    };
}
