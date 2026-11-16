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

    private static readonly (string TitleKey, Capability[] Capabilities)[] _groups =
    [
        ("GroupPlayback",
        [
            Capability.CanResumePlayback,
            Capability.CanReportPlaybackProgress,
            Capability.CanTranscode,
            Capability.CanScrobble
        ]),
        ("GroupContent", [Capability.CanRate, Capability.CanCreatePlaylist, Capability.CanShareMedia]),
        ("GroupHistory",
        [
            Capability.CanViewHistory,
            Capability.CanDeleteHistory,
            Capability.CanReassignHistory,
            Capability.CanViewStats
        ]),
        ("GroupAccount", [Capability.CanManageDevices, Capability.CanModifySettings]),
        ("GroupAdmin", [Capability.CanAccessAdmin, Capability.CanManageLibraries, Capability.CanManageUsers])
    ];

    private IReadOnlySet<Capability> _defaultCaps = DefaultCapabilities.ForRole(Roles.User);

    /// <summary>
    /// Capabilities by theme, so the list reads as a few short blocks. A capability added to the
    /// enum without a group still shows, under "Other".
    /// </summary>
    private IReadOnlyList<(string Title, IReadOnlyList<Capability> Capabilities)> Groups
    {
        get
        {
            var groups = _groups
                .Select(g => (Title: L[g.TitleKey].Value, Capabilities: (IReadOnlyList<Capability>)g.Capabilities))
                .ToList();

            var grouped = _groups.SelectMany(g => g.Capabilities).ToHashSet();
            var others = Enum.GetValues<Capability>().Where(c => !grouped.Contains(c)).ToList();
            if (others.Count > 0)
                groups.Add((L["GroupOther"].Value, others));

            return groups;
        }
    }

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
        Capability.CanDeleteHistory => L["CapCanDeleteHistory"],
        Capability.CanReassignHistory => L["CapCanReassignHistory"],
        Capability.CanManageDevices => L["CapCanManageDevices"],
        Capability.CanModifySettings => L["CapCanModifySettings"],
        Capability.CanAccessAdmin => L["CapCanAccessAdmin"],
        Capability.CanManageLibraries => L["CapCanManageLibraries"],
        Capability.CanManageUsers => L["CapCanManageUsers"],
        Capability.CanReportPlaybackProgress => L["CapCanReportPlaybackProgress"],
        Capability.CanTranscode => L["CapCanTranscode"],
        Capability.CanScrobble => L["CapCanScrobble"],
        Capability.CanShareMedia => L["CapCanShareMedia"],
        _ => cap.ToString()
    };
}
