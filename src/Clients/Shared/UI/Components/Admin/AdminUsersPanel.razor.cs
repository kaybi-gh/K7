using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminUsersPanel
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private bool _isLoading = true;
    private List<UserDto> _users = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _users = await K7ServerService.GetUsersAsync();
        }
        catch
        {
            _users = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnRoleChanged(UserDto user, string newRole)
    {
        try
        {
            await K7ServerService.UpdateUserRoleAsync(user.Id, new UpdateUserRoleRequest { Role = newRole });
            Snackbar.Add($"Rôle de {user.UserName} mis à jour.", Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
        }
    }

    private async Task OnToggleActive(UserDto user, bool isActive)
    {
        try
        {
            await K7ServerService.ToggleUserActiveAsync(user.Id, isActive);
            var label = user.UserName ?? user.Email ?? "Utilisateur";
            Snackbar.Add(isActive ? $"{label} activé." : $"{label} désactivé.", Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
        }
    }

    private async Task OpenCapabilitiesDialog(UserDto user)
    {
        var allCapabilities = Enum.GetValues<Capability>();
        var defaultCaps = DefaultCapabilities.ForRole(user.Role);

        var overrides = user.CapabilityOverrides
            .ToDictionary(o => o.Capability, o => o.Enabled);

        var parameters = new DialogParameters<AdminUserCapabilitiesDialog>
        {
            { x => x.UserName, user.UserName ?? user.Email ?? "Utilisateur" },
            { x => x.Role, user.Role },
            { x => x.Overrides, overrides }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AdminUserCapabilitiesDialog>("Capacités", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Dictionary<Capability, bool> newOverrides })
        {
            var request = new UpdateUserCapabilitiesRequest
            {
                Overrides = newOverrides
                    .Select(kv => new CapabilityOverrideDto { Capability = kv.Key, Enabled = kv.Value })
                    .ToList()
            };

            try
            {
                await K7ServerService.UpdateUserCapabilitiesAsync(user.Id, request);
                Snackbar.Add("Capacités mises à jour.", Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task ConfirmDelete(UserDto user)
    {
        var parameters = new DialogParameters<ConfirmDeleteUserDialog>
        {
            { x => x.DisplayName, user.UserName ?? user.Email ?? "cet utilisateur" }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteUserDialog>("Supprimer l'utilisateur", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await K7ServerService.DeleteUserAsync(user.Id);
                Snackbar.Add("Utilisateur supprimé.", Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task OpenLibraryExclusionsDialog(UserDto user)
    {
        var parameters = new DialogParameters<AdminUserLibraryExclusionsDialog>
        {
            { x => x.ExcludedLibraryIds, user.ExcludedLibraryIds }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AdminUserLibraryExclusionsDialog>("Accès aux librairies", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: List<Guid> newExclusions })
        {
            var request = new UpdateUserLibraryExclusionsRequest
            {
                ExcludedLibraryIds = newExclusions
            };

            try
            {
                await K7ServerService.UpdateUserLibraryExclusionsAsync(user.Id, request);
                Snackbar.Add("Accès aux librairies mis à jour.", Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task OpenMediaExclusionsDialog(UserDto user)
    {
        var parameters = new DialogParameters<AdminUserMediaExclusionsDialog>
        {
            { x => x.ExcludedMediaIds, user.ExcludedMediaIds }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AdminUserMediaExclusionsDialog>("Médias masqués", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: List<Guid> newExclusions })
        {
            var request = new UpdateUserMediaExclusionsRequest
            {
                ExcludedMediaIds = newExclusions
            };

            try
            {
                await K7ServerService.UpdateUserMediaExclusionsAsync(user.Id, request);
                Snackbar.Add("Médias masqués mis à jour.", Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task OpenRestrictionProfileDialog(UserDto user)
    {
        var parameters = new DialogParameters<AdminUserRestrictionProfileDialog>
        {
            { x => x.CurrentProfileId, user.ContentRestrictionProfileId }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AdminUserRestrictionProfileDialog>("Profil de restriction", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var profileId = result.Data as Guid?;
            try
            {
                await K7ServerService.AssignContentRestrictionProfileAsync(user.Id, profileId);
                Snackbar.Add("Profil de restriction mis à jour.", Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
            }
        }
    }
}
