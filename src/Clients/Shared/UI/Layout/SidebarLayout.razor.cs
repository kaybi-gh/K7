using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Layout;

public partial class SidebarLayout : IDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter, EditorRequired] public RenderFragment SidebarContent { get; set; } = default!;
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;

    private bool _sidebarOpen;
    private bool _desktopCollapsed;
    private bool _isTv;
    private bool _showDesktopCollapseToggle;
    private SidebarLayoutContext _sidebarContext = new(false);

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;

        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        _showDesktopCollapseToggle = !_isTv;

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

        return _desktopCollapsed ? "page-sidebar--collapsed" : string.Empty;
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
        _sidebarContext = new SidebarLayoutContext(_desktopCollapsed);

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
