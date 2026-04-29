using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Menu : IDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    [Parameter, EditorRequired] public RenderFragment ActivatorContent { get; set; } = default!;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }

    private bool _open;
    private ElementReference _root;
    private ElementReference _dropdown;
    private DotNetObjectReference<LayerCloseCallback>? _closeCallbackRef;

    internal async void Close()
    {
        if (!_open) return;
        _open = false;
        await OpenChanged.InvokeAsync(false);
        try
        {
            await SpatialNav.PopLayerAsync(_dropdown);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // Element already removed
        }
        StateHasChanged();
    }

    private async Task Toggle()
    {
        _open = !_open;
        await OpenChanged.InvokeAsync(_open);

        if (_open)
        {
            StateHasChanged();
            await Task.Yield();
            _closeCallbackRef?.Dispose();
            _closeCallbackRef = DotNetObjectReference.Create(new LayerCloseCallback(Close));
            try
            {
                await SpatialNav.PushLayerAsync(_dropdown, "popover", new SpatialNavLayerOptions
                {
                    OnClose = _closeCallbackRef
                });
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                // Element not yet rendered
            }
        }
        else
        {
            try
            {
                await SpatialNav.PopLayerAsync(_dropdown);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                // Element already removed
            }
        }
    }

    protected override void OnParametersSet()
    {
        if (OpenChanged.HasDelegate)
            _open = Open;
    }

    public void Dispose()
    {
        _closeCallbackRef?.Dispose();
    }
}
