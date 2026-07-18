using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace K7.Clients.Shared.UI.Components.TvHub;

public partial class TvHubPageHost : IDisposable
{
    protected override void OnInitialized()
    {
        TvHubHost.Changed += OnHostChanged;
        Navigation.LocationChanged += OnLocationChanged;
        TvHubHost.UpdateLocation(Navigation.Uri);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
        TvHubHost.UpdateLocation(e.Location);

    private void OnHostChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        TvHubHost.Changed -= OnHostChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
