using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Menu : IAsyncDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public RenderFragment ActivatorContent { get; set; } = default!;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string? Title { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    [Parameter] public ElementReference PositionAnchor { get; set; }
    [Parameter] public bool HasPositionAnchor { get; set; }

    private bool _open;
    private bool _layerPushed;
    private bool _mobileMenuAttached;
    private ElementReference _root;
    private ElementReference _dropdown;
    private ElementReference _backdrop;
    private DotNetObjectReference<LayerCloseCallback>? _closeCallbackRef;

    internal void Close() => CloseAsync().FireAndForget();

    private async Task CloseAsync()
    {
        if (!_open) return;
        await CloseMenuInternalAsync();
        _open = false;
        await OpenChanged.InvokeAsync(false);
        StateHasChanged();
    }

    private async Task Toggle()
    {
        if (Disabled)
            return;

        _open = !_open;
        await OpenChanged.InvokeAsync(_open);

        if (_open)
            await OpenMenuInternalAsync();
        else
            await CloseMenuInternalAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Disabled && _open)
        {
            await CloseMenuInternalAsync();
            _open = false;
            await OpenChanged.InvokeAsync(false);
        }

        if (!OpenChanged.HasDelegate)
        {
            if (_open == Open)
                return;

            _open = Open && !Disabled;

            if (_open)
                await OpenMenuInternalAsync();
            else
                await CloseMenuInternalAsync();

            return;
        }

        if (_open == Open)
            return;

        _open = Open && !Disabled;

        if (_open)
            await OpenMenuInternalAsync();
        else
            await CloseMenuInternalAsync();
    }

    private async Task OpenMenuInternalAsync()
    {
        _closeCallbackRef?.Dispose();
        _closeCallbackRef = DotNetObjectReference.Create(new LayerCloseCallback(Close));
        await InvokeAsync(StateHasChanged);
        try
        {
            await SpatialNav.AttachLayerCallbackAsync(_dropdown, _closeCallbackRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_open)
        {
            _layerPushed = false;
            if (_mobileMenuAttached)
            {
                try
                {
                    await DetachMobileMenuAsync();
                }
                catch (Exception ex) when (ex is JSException or InvalidOperationException)
                {
                }
            }

            return;
        }

        try
        {
            if (HasPositionAnchor)
                await JS.InvokeVoidAsync("K7.setMenuPositionAnchor", PositionAnchor);
            else
                await JS.InvokeVoidAsync("K7.clearMenuPositionAnchor");

            await JS.InvokeVoidAsync("K7.attachMobileMenu", _root, _dropdown, _backdrop);
            _mobileMenuAttached = true;
            await JS.InvokeVoidAsync("K7.positionDropdownDeferred", _root, _dropdown);

            if (!_layerPushed)
            {
                _layerPushed = true;
                await SpatialNav.PushLayerAsync(_dropdown, "popover", new SpatialNavLayerOptions
                {
                    OnClose = _closeCallbackRef
                });
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // Element not yet rendered
        }
    }

    private async Task DetachMobileMenuAsync()
    {
        await JS.InvokeVoidAsync("K7.detachMobileMenu", _root, _dropdown, _backdrop);
        _mobileMenuAttached = false;
    }

    private async Task CloseMenuInternalAsync()
    {
        _layerPushed = false;
        try
        {
            await JS.InvokeVoidAsync("K7.clearMenuPositionAnchor");
            await JS.InvokeVoidAsync("K7.resetDropdown", _root);
            if (_mobileMenuAttached)
                await DetachMobileMenuAsync();
            await SpatialNav.PopLayerAsync(_dropdown);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // Element already removed
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_open)
        {
            try
            {
                if (_mobileMenuAttached)
                    await DetachMobileMenuAsync();
                await SpatialNav.PopLayerAsync(_dropdown);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
        else if (_mobileMenuAttached)
        {
            try
            {
                await DetachMobileMenuAsync();
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }

        _closeCallbackRef?.Dispose();
    }
}
