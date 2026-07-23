using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace K7.Clients.Shared.UI.Components.FeedHub;

public partial class FeedHubPageHost : IDisposable
{
    // inert must be omitted when active: inert="false" is still inert in HTML.
    private static readonly Dictionary<string, object> InactivePageAttributes =
        new() { ["inert"] = true };

    protected override void OnInitialized()
    {
        FeedHub.Changed += OnHostChanged;
        Navigation.LocationChanged += OnLocationChanged;
        FeedHub.UpdateLocation(Navigation.Uri);
    }

    private static IReadOnlyDictionary<string, object>? GetInactivePageAttributes(bool isActive) =>
        isActive ? null : InactivePageAttributes;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
        FeedHub.UpdateLocation(e.Location);

    private void OnHostChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        FeedHub.Changed -= OnHostChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
