using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Select<TValue> : IAsyncDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? SelectedContent { get; set; }
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }
    [Parameter] public Func<TValue?, string>? ToStringFunc { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Placeholder { get; set; } = "";
    [Parameter] public string? HelperText { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Clearable { get; set; }
    [Parameter] public bool FullWidth { get; set; } = true;
    [Parameter] public string Variant { get; set; } = "outlined";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";

    private bool _open;
    private bool _dropdownPlaced;
    private bool _dropdownTeleported;
    private ElementReference _root;
    private ElementReference _button;
    private ElementReference _dropdown;
    private ElementReference _backdrop;
    private DotNetObjectReference<LayerCloseCallback>? _closeCallbackRef;
    private readonly List<SelectItemRegistration<TValue>> _items = [];

    internal string DisplayText
    {
        get
        {
            if (ToStringFunc is not null)
                return ToStringFunc(Value) ?? "";

            var item = SelectedItem;
            if (item?.DisplayText is not null)
                return item.DisplayText;

            if (Value is null)
                return string.IsNullOrEmpty(Placeholder) ? "" : Placeholder;

            return Value.ToString() ?? "";
        }
    }

    private SelectItemRegistration<TValue>? SelectedItem =>
        _items.FirstOrDefault(i => EqualityComparer<TValue>.Default.Equals(i.Value, Value));

    /// <summary>
    /// Markup of the selected item when it has no <c>Text</c>: shown in the trigger as is, so an
    /// item written as content (name plus a secondary text) does not fall back to its raw value,
    /// such as an id.
    /// </summary>
    private RenderFragment? SelectedItemContent =>
        ToStringFunc is null && SelectedItem is { HasText: false, Content: { } content } ? content : null;

    internal void RegisterItem(SelectItemRegistration<TValue> item)
    {
        _items.Add(item);
        StateHasChanged();
    }

    internal void UnregisterItem(SelectItemRegistration<TValue> item)
    {
        _items.Remove(item);
    }

    internal bool IsSelected(TValue? value)
    {
        return EqualityComparer<TValue>.Default.Equals(Value, value);
    }

    internal async Task SelectValueAsync(TValue? value)
    {
        await ValueChanged.InvokeAsync(value);
        await CloseAsync();
    }

    private async Task ClearAsync()
    {
        await ValueChanged.InvokeAsync(default);
    }

    private async Task Toggle()
    {
        if (_open)
            await CloseAsync();
        else
            await OpenAsync();
    }

    private async Task OpenAsync()
    {
        _open = true;
        _dropdownPlaced = false;
        _dropdownTeleported = false;
        StateHasChanged();
        await Task.Yield();
        _closeCallbackRef?.Dispose();
        _closeCallbackRef = DotNetObjectReference.Create(new LayerCloseCallback(OnLayerClosed));
        try
        {
            await JS.InvokeVoidAsync("K7.attachSelectPortal", _root, _dropdown, _backdrop);
            await JS.InvokeVoidAsync("K7.positionSelectDropdown", _button, _dropdown);
            _dropdownTeleported = true;
            _dropdownPlaced = true;
            await SpatialNav.PushLayerAsync(_dropdown, "popover", new SpatialNavLayerOptions
            {
                OnClose = _closeCallbackRef
            });
            StateHasChanged();
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
    }

    private async Task CloseAsync()
    {
        if (!_open) return;
        _open = false;
        _dropdownPlaced = false;
        _dropdownTeleported = false;
        try
        {
            await JS.InvokeVoidAsync("K7.detachSelectPortal", _root, _dropdown, _backdrop);
            await JS.InvokeVoidAsync("K7.releaseMobileOverlayLock", _root);
            await SpatialNav.PopLayerAsync(_dropdown);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
        StateHasChanged();
    }

    private void OnLayerClosed() => OnLayerClosedAsync().FireAndForget();

    private async Task OnLayerClosedAsync()
    {
        if (!_open) return;
        _open = false;
        _dropdownPlaced = false;
        _dropdownTeleported = false;
        try
        {
            await JS.InvokeVoidAsync("K7.detachSelectPortal", _root, _dropdown, _backdrop);
            await JS.InvokeVoidAsync("K7.releaseMobileOverlayLock", _root);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_open)
        {
            try
            {
                await JS.InvokeVoidAsync("K7.detachSelectPortal", _root, _dropdown, _backdrop);
                await SpatialNav.PopLayerAsync(_dropdown);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
        _closeCallbackRef?.Dispose();
    }
}

public sealed class SelectItemRegistration<TValue>(TValue? value, string? displayText, bool hasText = true)
{
    public TValue? Value { get; } = value;
    public string? DisplayText { get; } = displayText;

    /// <summary>False when the item gave no <c>Text</c> and <see cref="DisplayText"/> is only its raw value.</summary>
    public bool HasText { get; } = hasText;

    /// <summary>Latest markup of the item, refreshed on each render of the item.</summary>
    public RenderFragment? Content { get; set; }
}
