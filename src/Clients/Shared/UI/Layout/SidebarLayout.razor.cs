using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class SidebarLayout : IAsyncDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter, EditorRequired] public RenderFragment SidebarContent { get; set; } = default!;
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;

    private bool _sidebarOpen;
    private bool _desktopCollapsed;
    private bool _isMobileViewport = true;
    private bool _isPhoneDevice;
    private bool _isTv;
    private bool _showDesktopCollapseToggle;
    private bool _disposed;
    private SidebarLayoutContext _sidebarContext = new(false);
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SidebarLayout>? _dotnetRef;

    private bool UseMobileDrawer => _isMobileViewport || _isPhoneDevice;
    private bool IsCollapsed => _desktopCollapsed && !UseMobileDrawer;

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;

        var deviceType = await DeviceService.GetDeviceTypeAsync();
        _isTv = deviceType == DeviceType.TV;
        _isPhoneDevice = deviceType == DeviceType.Phone;
        _showDesktopCollapseToggle = !_isTv && !_isPhoneDevice;

        // Non-phone hosts use the desktop sidebar on first render; JS may switch to mobile drawer later.
        if (!_isPhoneDevice)
        {
            _isMobileViewport = false;
        }

        if (_showDesktopCollapseToggle)
        {
            _desktopCollapsed = DeviceStorage.Get(PreferenceKeys.PAGE_SIDEBAR_COLLAPSED, false);
        }

        UpdateSidebarContext();

        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        if (uri.Query.Contains("sidebar=open", StringComparison.OrdinalIgnoreCase))
        {
            _sidebarOpen = true;
        }
    }

    private string GetSidebarClass()
    {
        if (_sidebarOpen)
        {
            return "sidebar-open";
        }

        return IsCollapsed ? "page-sidebar--collapsed" : string.Empty;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            _jsModule = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");
            _dotnetRef ??= DotNetObjectReference.Create(this);
            _isMobileViewport = await _jsModule.InvokeAsync<bool>("observeViewport", _dotnetRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            _isMobileViewport = false;
        }

        UpdateSidebarContext();
        StateHasChanged();
    }

    [JSInvokable]
    public Task OnViewportChanged(bool isMobile)
    {
        if (_disposed || _isMobileViewport == isMobile)
        {
            return Task.CompletedTask;
        }

        _isMobileViewport = isMobile;
        UpdateSidebarContext();
        return InvokeAsync(StateHasChanged);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_sidebarOpen)
        {
            _sidebarOpen = false;
            InvokeAsync(StateHasChanged);
        }
    }

    private void ToggleSidebar() => _sidebarOpen = !_sidebarOpen;

    private void ToggleDesktopCollapse()
    {
        _desktopCollapsed = !_desktopCollapsed;
        DeviceStorage.Set(PreferenceKeys.PAGE_SIDEBAR_COLLAPSED, _desktopCollapsed);
        UpdateSidebarContext();
        StateHasChanged();
    }

    private void UpdateSidebarContext() =>
        _sidebarContext = new SidebarLayoutContext(IsCollapsed);

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        NavigationManager.LocationChanged -= OnLocationChanged;

        var dotnetRef = _dotnetRef;
        var jsModule = _jsModule;
        _dotnetRef = null;
        _jsModule = null;

        if (jsModule is not null)
        {
            try
            {
                if (dotnetRef is not null)
                {
                    await jsModule.InvokeVoidAsync("disposeViewport", dotnetRef);
                }

                await jsModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
            {
            }
        }

        dotnetRef?.Dispose();
    }
}
