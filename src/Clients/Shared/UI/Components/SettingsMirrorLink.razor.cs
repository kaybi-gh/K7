using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Link between a personal settings page and the admin page holding its server defaults, on the
/// same sub-page. Shown in the page header. The link towards the admin only shows to accounts
/// that can open it, and pages without a counterpart render nothing.
/// </summary>
public partial class SettingsMirrorLink
{
    private string? _href;
    private bool _toAdmin;
    private bool _canAccessAdmin;

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        Navigation.LocationChanged += OnLocationChanged;
        Resolve();

        try
        {
            _canAccessAdmin = await FeatureAccess.HasCapabilityAsync(Capability.CanAccessAdmin);
        }
        catch
        {
            // Best effort: no link towards the admin.
        }
    }

    private void Resolve() => _href = SettingsMirror.GetCounterpart(CustomNavPath.From(Navigation), out _toAdmin);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        Resolve();
        InvokeAsync(StateHasChanged);
    }
}
