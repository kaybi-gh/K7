using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class SidebarNavGroup : IDisposable, IAsyncDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    [CascadingParameter] private SidebarLayoutContext? SidebarContext { get; set; }

    [Parameter] public required string Title { get; set; }
    [Parameter] public string? Icon { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public IEnumerable<string> Routes { get; set; } = [];

    private bool _flyoutOpen;
    private bool _layerPushed;
    private ElementReference _triggerRef;
    private ElementReference _flyoutRef;
    private DotNetObjectReference<LayerCloseCallback>? _closeCallbackRef;

    private bool IsDesktopCollapsed => SidebarContext?.IsDesktopCollapsed == true;

    private bool IsOpen => Routes.Any(IsRouteActive);

    private string CssClass => IsOpen ? "nav-group--active" : string.Empty;

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!IsDesktopCollapsed && _flyoutOpen)
        {
            await CloseFlyoutAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_flyoutOpen)
        {
            _layerPushed = false;
            return;
        }

        if (_layerPushed)
        {
            return;
        }

        try
        {
            if (_closeCallbackRef is not null)
            {
                await SpatialNav.AttachLayerCallbackAsync(_flyoutRef, _closeCallbackRef);
            }

            await SpatialNav.PushLayerAsync(_flyoutRef, "popover", new SpatialNavLayerOptions
            {
                OnClose = _closeCallbackRef,
                RestoreFocus = _triggerRef,
                FocusSelector = ".nav-link.focusable"
            });
            _layerPushed = true;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
    }

    private async Task OnTriggerActivateAsync()
    {
        if (!IsDesktopCollapsed)
        {
            return;
        }

        if (_flyoutOpen)
        {
            try
            {
                await SpatialNav.FocusFirstAsync(".nav-link.focusable");
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
            }

            return;
        }

        _closeCallbackRef?.Dispose();
        _closeCallbackRef = DotNetObjectReference.Create(new LayerCloseCallback(CloseFlyoutFromLayer));
        _flyoutOpen = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnFlyoutKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is "Escape")
        {
            await CloseFlyoutAsync();
        }
    }

    private void CloseFlyoutFromLayer()
    {
        InvokeAsync(CloseFlyoutAsync).FireAndForget();
    }

    private async Task CloseFlyoutAsync()
    {
        if (!_flyoutOpen)
        {
            return;
        }

        _flyoutOpen = false;

        if (_layerPushed)
        {
            _layerPushed = false;
            try
            {
                await SpatialNav.PopLayerAsync(_flyoutRef);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
            }
        }

        _closeCallbackRef?.Dispose();
        _closeCallbackRef = null;

        try
        {
            await _triggerRef.FocusAsync();
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }

        await InvokeAsync(StateHasChanged);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_flyoutOpen)
        {
            InvokeAsync(CloseFlyoutAsync).FireAndForget();
        }
    }

    private bool IsRouteActive(string route)
    {
        var relative = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        var path = relative.Split('?', '#')[0];
        return path.Equals(route.TrimStart('/'), StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(route.TrimStart('/') + "/", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }

    public async ValueTask DisposeAsync()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        _closeCallbackRef?.Dispose();

        if (_layerPushed)
        {
            try
            {
                await SpatialNav.PopLayerAsync(_flyoutRef);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
            }
        }
    }
}
