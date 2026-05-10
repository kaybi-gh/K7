using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace K7.Clients.Shared.UI.Layout;

public partial class SidebarLayout : IDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter, EditorRequired] public RenderFragment SidebarContent { get; set; } = default!;
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;

    private bool _sidebarOpen;

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;

        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var query = uri.Query;
        if (query.Contains("sidebar=open", StringComparison.OrdinalIgnoreCase))
        {
            _sidebarOpen = true;
        }
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

    private void CloseSidebar() => _sidebarOpen = false;

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
