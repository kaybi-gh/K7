using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class EpisodeListItem : IDisposable
{
    private const int LongPressDelayMs = 600;
    private const double LongPressMoveThresholdSquared = 100;

    [Inject] private IStringLocalizer<SharedResource> SharedStrings { get; set; } = default!;
    [Inject] private IStringLocalizer<EpisodeListItem> L { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private MediaCacheStore CacheStore { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<EpisodeListItem> Logger { get; set; } = default!;

    [Parameter, EditorRequired]
    public required LiteSerieEpisodeDto Episode { get; set; }

    [Parameter]
    public string? StillUrl { get; set; }

    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public EventCallback<LiteSerieEpisodeDto> OnPlay { get; set; }

    [Parameter]
    public EventCallback<LiteSerieEpisodeDto> OnWatchStateChanged { get; set; }

    private bool _menuOpen;
    private bool _longPressTriggered;
    private bool _preventNextClick;
    private bool _keyHeldDown;
    private bool _menuOpenedViaKeyboard;
    private CancellationTokenSource? _longPressCts;
    private double _touchStartX;
    private double _touchStartY;

    private bool ShouldPreventLinkActivation =>
        _preventNextClick || _keyHeldDown || _longPressTriggered || _menuOpen;

    private Task PlayAsync() => OnPlay.HasDelegate
        ? OnPlay.InvokeAsync(Episode)
        : Task.CompletedTask;

    private Task NavigateToDetailAsync()
    {
        if (!string.IsNullOrEmpty(Href))
            NavigationManager.NavigateTo(Href);
        return Task.CompletedTask;
    }

    private async Task ToggleWatchStateAsync()
    {
        var watched = Episode.UserState?.IsCompleted != true;
        var success = await WatchStateActions.ApplyAsync(
            MediaService,
            CacheStore,
            DialogService,
            Snackbar,
            SharedStrings,
            Episode.Id,
            watched,
            WatchStateScope.Item);

        if (!success)
            return;

        if (OnWatchStateChanged.HasDelegate)
            await OnWatchStateChanged.InvokeAsync(Episode);
    }

    private async Task OnMenuOpenChangedAsync(bool open)
    {
        _menuOpen = open;
        if (!open)
        {
            _longPressTriggered = false;
            _preventNextClick = false;
            _menuOpenedViaKeyboard = false;
            _keyHeldDown = false;
            CancelLongPress();
        }

        await Task.CompletedTask;
    }

    private async Task OpenMenuAsync()
    {
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
            catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException)
            {
            }
        }

        if (_menuOpen)
            return;

        _menuOpen = true;
        StateHasChanged();
    }

    private void OnLinkFocusIn(FocusEventArgs e) => SyncEpisodeAnchorInUrl();

    private void SyncEpisodeAnchorInUrl()
    {
        try
        {
            _ = JS.InvokeVoidAsync("K7.replaceUrlHash", $"ep-{Episode.EpisodeNumber}");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException)
        {
        }
    }

    private void OnContextMenu(MouseEventArgs e)
    {
        _longPressTriggered = true;
        _preventNextClick = true;
        OpenMenuAsync().FireAndForget(Logger);
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
        if (!IsEnterKey(e))
            return;

        if (e.Repeat && _longPressCts is not null)
            return;

        _keyHeldDown = true;
        CancelLongPress();
        _longPressTriggered = false;
        _longPressCts = new CancellationTokenSource();
        WaitForLongPressAsync(_longPressCts.Token, fromKeyboard: true).FireAndForget(Logger);
    }

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (!IsEnterKey(e))
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

    private void OnTouchStart(TouchEventArgs e)
    {
        if (e.Touches.Length == 0)
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
            // Open on the renderer sync context so the menu paints immediately
            // (not only after the next unrelated key event).
            await InvokeAsync(async () =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _longPressTriggered = true;
                _preventNextClick = true;

                if (fromKeyboard)
                {
                    _menuOpenedViaKeyboard = true;
                    _keyHeldDown = false;
                }

                await OpenMenuAsync();
            });
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

    private void OnLinkClick(MouseEventArgs e)
    {
        if (ShouldPreventLinkActivation)
            _preventNextClick = true;
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h{ts.Minutes:00}"
            : $"{ts.Minutes}min";
    }

    public void Dispose() => CancelLongPress();
}
