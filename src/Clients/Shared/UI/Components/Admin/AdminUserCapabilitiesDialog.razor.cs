using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminUserCapabilitiesDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public string UserName { get; set; } = "";
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

    private void Cancel() => MudDialog.Cancel();
    private void Submit() => MudDialog.Close(DialogResult.Ok(Overrides));

    private static string GetRoleLabel(string role) => role switch
    {
        Roles.Administrator => "Administrateur",
        Roles.User => "Utilisateur",
        Roles.Guest => "Invité",
        _ => role
    };

    private static string GetCapabilityLabel(Capability cap) => cap switch
    {
        Capability.CanRate => "Noter les médias",
        Capability.CanCreatePlaylist => "Créer des playlists",
        Capability.CanViewStats => "Voir les statistiques",
        Capability.CanResumePlayback => "Reprendre la lecture",
        Capability.CanViewHistory => "Voir l'historique",
        Capability.CanManageDevices => "Gérer les appareils",
        Capability.CanModifySettings => "Modifier les paramètres",
        Capability.CanAccessAdmin => "Accéder à l'admin",
        Capability.CanManageLibraries => "Gérer les librairies",
        Capability.CanManageUsers => "Gérer les utilisateurs",
        Capability.CanReportPlaybackProgress => "Reporter la progression",
        _ => cap.ToString()
    };
}
