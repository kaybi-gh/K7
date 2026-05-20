using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Select<TValue> : IAsyncDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? SelectedContent { get; set; }
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }
    [Parameter] public Func<TValue?, string>? ToStringFunc { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Placeholder { get; set; } = "";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Clearable { get; set; }
    [Parameter] public bool FullWidth { get; set; } = true;
    [Parameter] public string Variant { get; set; } = "outlined";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";

    private bool _open;
    private ElementReference _root;
    private ElementReference _dropdown;
    private DotNetObjectReference<LayerCloseCallback>? _closeCallbackRef;
    private readonly List<SelectItemRegistration<TValue>> _items = [];

    internal string DisplayText
    {
        get
        {
            if (ToStringFunc is not null)
                return ToStringFunc(Value) ?? "";

            var item = _items.FirstOrDefault(i => EqualityComparer<TValue>.Default.Equals(i.Value, Value));
            if (item?.DisplayText is not null)
                return item.DisplayText;

            if (Value is null)
                return string.IsNullOrEmpty(Placeholder) ? "" : Placeholder;

            return Value.ToString() ?? "";
        }
    }

    internal void RegisterItem(SelectItemRegistration<TValue> item)
    {
        _items.Add(item);
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
        StateHasChanged();
        await Task.Yield();
        _closeCallbackRef?.Dispose();
        _closeCallbackRef = DotNetObjectReference.Create(new LayerCloseCallback(OnLayerClosed));
        try
        {
            await SpatialNav.PushLayerAsync(_dropdown, "popover", new SpatialNavLayerOptions
            {
                OnClose = _closeCallbackRef
            });
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
    }

    private async Task CloseAsync()
    {
        if (!_open) return;
        _open = false;
        try
        {
            await SpatialNav.PopLayerAsync(_dropdown);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
        StateHasChanged();
    }

    private async void OnLayerClosed()
    {
        if (!_open) return;
        _open = false;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_open)
        {
            try { await SpatialNav.PopLayerAsync(_dropdown); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
        _closeCallbackRef?.Dispose();
    }
}

public sealed class SelectItemRegistration<TValue>(TValue? value, string? displayText)
{
    public TValue? Value { get; } = value;
    public string? DisplayText { get; } = displayText;
}
