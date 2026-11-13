using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Shell of the Interface settings: one index (appearance, home layout, navigation, theme songs)
/// shared by the pages that edit them, for the personal settings and for the server defaults.
/// Each page renders its own form as <see cref="ChildContent"/>.
/// </summary>
public partial class InterfaceSettingsSplit
{
    private bool _isGuest;

    /// <summary>Sub-page being rendered, one of <see cref="InterfaceSettingsSections"/>.</summary>
    [Parameter, EditorRequired] public string Section { get; set; } = InterfaceSettingsSections.Appearance;

    /// <summary>True for the server defaults (admin), false for the personal settings.</summary>
    [Parameter] public bool Admin { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Inject] private IUserAdminService UserService { get; set; } = default!;

    private string BasePath => Admin ? InterfaceSettingsSections.AdminBasePath : InterfaceSettingsSections.SettingsBasePath;

    private string ActiveTitle => Section switch
    {
        InterfaceSettingsSections.Home => L["Home"],
        InterfaceSettingsSections.Navigation => L["Navigation"],
        InterfaceSettingsSections.ThemeSongs => L["ThemeSongs"],
        _ => L["Appearance"]
    };

    /// <summary>False on the bare index address, which shows the index alone on narrow screens and TV.</summary>
    private bool DetailOpen =>
        !string.Equals(CustomNavPath.From(Navigation), BasePath, StringComparison.OrdinalIgnoreCase);

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        Navigation.LocationChanged += OnLocationChanged;

        if (Admin)
            return;

        // Guests have no home layout or navigation of their own.
        try
        {
            _isGuest = (await UserService.GetCurrentUserAsync())?.IsGuest == true;
        }
        catch
        {
            // Best effort: keep every entry.
        }
    }

    // A page that serves several addresses is not rendered again when only the address
    // changes, so the shell follows the address itself to open and close the sub-page.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);
}
