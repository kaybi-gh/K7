using K7.Clients.Shared.UI;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AdminUserCapabilitiesDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public string Role { get; set; } = Roles.User;
    [Parameter] public Dictionary<Capability, bool> Overrides { get; set; } = new();

    private IReadOnlySet<Capability> _defaultCaps = DefaultCapabilities.ForRole(Roles.User);

    protected override void OnParametersSet()
    {
        _defaultCaps = DefaultCapabilities.ForRole(Role);
        Overrides = new Dictionary<Capability, bool>(Overrides);
    }

    private void SetOverride(Capability capability, bool value, bool defaultEnabled)
    {
        if (value == defaultEnabled)
            Overrides.Remove(capability);
        else
            Overrides[capability] = value;
    }

    private void RemoveOverride(Capability capability)
    {
        Overrides.Remove(capability);
    }

    private void Cancel() => Dialog.Cancel();
    private void Submit() => Dialog.Close(K7DialogResult.Ok(Overrides));

    private string GetRoleLabel(string role) => role switch
    {
        Roles.Administrator => L["RoleAdministrator"],
        Roles.User => L["RoleUser"],
        Roles.Guest => L["RoleGuest"],
        _ => role
    };

    private string GetCapabilityLabel(Capability cap) => cap switch
    {
        Capability.CanRate => L["CapCanRate"],
        Capability.CanCreatePlaylist => L["CapCanCreatePlaylist"],
        Capability.CanViewStats => L["CapCanViewStats"],
        Capability.CanResumePlayback => L["CapCanResumePlayback"],
        Capability.CanViewHistory => L["CapCanViewHistory"],
        Capability.CanManageDevices => L["CapCanManageDevices"],
        Capability.CanModifySettings => L["CapCanModifySettings"],
        Capability.CanAccessAdmin => L["CapCanAccessAdmin"],
        Capability.CanManageLibraries => L["CapCanManageLibraries"],
        Capability.CanManageUsers => L["CapCanManageUsers"],
        Capability.CanReportPlaybackProgress => L["CapCanReportPlaybackProgress"],
        Capability.CanTranscode => L["CapCanTranscode"],
        _ => cap.ToString()
    };
}
