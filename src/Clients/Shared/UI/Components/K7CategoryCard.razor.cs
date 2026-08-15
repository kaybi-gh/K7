using K7.Clients.Shared.Helpers;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7CategoryCard : IDisposable
{
    private const int LongPressDelayMs = 600;
    private const double LongPressMoveThresholdSquared = 100;

    private readonly Guid _menuOwnerId = Guid.NewGuid();
    private bool _menuOpen;
    private bool _longPressTriggered;
    private bool _keyHeldDown;
    private bool _menuOpenedViaKeyboard;
    private bool _preventNextClick;
    private bool _longPressRegistered;
    private CancellationTokenSource? _longPressCts;
    private double _touchStartX;
    private double _touchStartY;
    private ElementReference _longPressContainerRef;
    private DotNetObjectReference<K7CategoryCard>? _longPressDotNetRef;

    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<K7CategoryCard> Logger { get; set; } = default!;
    [Inject] private IMediaCardContextMenuService ContextMenuService { get; set; } = default!;

    /// <summary>Primary title displayed in large bold italic caps.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    /// <summary>Secondary line displayed below the title in small uppercase.</summary>
    [Parameter] public string Description { get; set; } = string.Empty;

    /// <summary>Phosphor icon name (without the "ph-" prefix).</summary>
    [Parameter] public string Icon { get; set; } = string.Empty;

    /// <summary>CSS color used as the gradient tone when CardColor is unset.</summary>
    [Parameter] public string GradientStart { get; set; } = "rgba(80,20,20,0.85)";

    /// <summary>Background color of the icon badge.</summary>
    [Parameter] public string IconColor { get; set; } = "rgba(0,0,0,0.55)";

    /// <summary>Optional background image URL. Displayed behind the gradient overlay.</summary>
    [Parameter] public string? ImageUrl { get; set; }

    /// <summary>Configured library-group card color (hex). Falls back to GradientStart when unset.</summary>
    [Parameter] public string? CardColor { get; set; }

    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public RenderFragment? ContextMenu { get; set; }
    [Parameter] public string? ContextMenuTitle { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool ContextMenuEnabled => ContextMenu is not null;

    private bool ShouldPreventActivation =>
        _preventNextClick || _keyHeldDown || _longPressTriggered || _menuOpen;

    private string? ResolvedImageUrl => MediaPictureUrlHelper.ToDisplayUrl(ApiClient, ImageUrl);

    private string ComputedTone => ResolveOpaqueTone(
        !string.IsNullOrWhiteSpace(CardColor) ? CardColor : GradientStart);

    protected override void OnInitialized() =>
        ContextMenuService.Changed += OnContextMenuServiceChanged;

    private static string ResolveOpaqueTone(string color)
    {
        var trimmed = color.Trim();

        if (trimmed.StartsWith('#'))
        {
            var hex = trimmed[1..];
            return hex.Length >= 6 ? $"#{hex[..6]}" : trimmed;
        }

        if (trimmed.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = trimmed[5..^1];
            var parts = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && int.TryParse(parts[0], out var r)
                && int.TryParse(parts[1], out var g)
                && int.TryParse(parts[2], out var b))
                return $"#{r:X2}{g:X2}{b:X2}";
        }

        if (trimmed.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = trimmed[4..^1];
            var parts = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && int.TryParse(parts[0], out var r)
                && int.TryParse(parts[1], out var g)
                && int.TryParse(parts[2], out var b))
                return $"#{r:X2}{g:X2}{b:X2}";
        }

        return trimmed;
    }

    private async Task OnFocusInAsync() => await EnsureLongPressRegisteredAsync();

    private async Task EnsureLongPressRegisteredAsync()
    {
        if (_longPressRegistered || !ContextMenuEnabled)
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
            _keyHeldDown = false;
            CancelLongPress();
        }

        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OpenContextMenuFromLongPressAsync()
    {
        if (!ContextMenuEnabled)
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
        if (!ContextMenuEnabled)
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

        ContextMenuService.Open(new MediaCardContextMenuRequest
        {
            OwnerId = _menuOwnerId,
            Anchor = _longPressContainerRef,
            AnchorKind = MediaCardContextMenuAnchorKind.Card,
            Title = string.IsNullOrWhiteSpace(ContextMenuTitle) ? Title : ContextMenuTitle,
            Content = ContextMenu
        });

        return Task.CompletedTask;
    }

    private void OnContextMenu(MouseEventArgs e)
    {
        if (!ContextMenuEnabled)
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
        if (!ContextMenuEnabled || !IsEnterKey(e))
            return;

        if (e.Repeat && _longPressCts is not null)
            return;

        _ = EnsureLongPressRegisteredAsync();

        _keyHeldDown = true;
        CancelLongPress();
        _longPressTriggered = false;
        _longPressCts = new CancellationTokenSource();
        WaitForLongPressAsync(_longPressCts.Token, fromKeyboard: true).FireAndForget(Logger);
    }

    private async Task OnKeyUp(KeyboardEventArgs e)
    {
        if (!ContextMenuEnabled || !IsEnterKey(e))
            return;

        CancelLongPress();

        var wasShortPress = _keyHeldDown && !_longPressTriggered;
        _keyHeldDown = false;

        if (_longPressTriggered)
        {
            _preventNextClick = true;
            return;
        }

        if (wasShortPress)
            await ActivateAsync();
    }

    private void OnTouchStart(TouchEventArgs e)
    {
        if (!ContextMenuEnabled || e.Touches.Length == 0)
            return;

        _ = EnsureLongPressRegisteredAsync();

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

    private async Task OnHitClick()
    {
        if (ContextMenuEnabled && ShouldPreventActivation)
        {
            if (_longPressTriggered || _menuOpen || _keyHeldDown)
                _preventNextClick = true;
            return;
        }

        await ActivateAsync();
    }

    private Task ActivateAsync() =>
        OnClick.HasDelegate ? OnClick.InvokeAsync() : Task.CompletedTask;

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
        _longPressDotNetRef = null;
    }
}
