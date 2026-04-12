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
    private Guid? _currentUserId;

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
            if (_currentUserId is null)
            {
                var me = await K7ServerService.GetCurrentUserAsync();
                _currentUserId = me?.Id;
            }
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
            Snackbar.Add(string.Format(L["RoleUpdated"], user.UserName), Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
    }

    private async Task OnToggleActive(UserDto user, bool isActive)
    {
        try
        {
            await K7ServerService.ToggleUserActiveAsync(user.Id, isActive);
            var label = user.UserName ?? user.Email;
            Snackbar.Add(isActive ? string.Format(L["UserActivated"], label) : string.Format(L["UserDeactivated"], label), Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
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
            { x => x.UserName, user.UserName ?? user.Email ?? "" },
            { x => x.Role, user.Role },
            { x => x.Overrides, overrides }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AdminUserCapabilitiesDialog>(L["CapabilitiesTitle"], parameters, options);
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
                Snackbar.Add(L["CapabilitiesUpdated"], Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
            }
        }
    }

    private async Task ConfirmDelete(UserDto user)
    {
        var parameters = new DialogParameters<ConfirmDeleteUserDialog>
        {
            { x => x.DisplayName, user.UserName ?? user.Email ?? L["DeleteFallbackName"] }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteUserDialog>(L["DeleteUserTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await K7ServerService.DeleteUserAsync(user.Id);
                Snackbar.Add(L["UserDeleted"], Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
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
        var dialog = await DialogService.ShowAsync<AdminUserLibraryExclusionsDialog>(L["LibraryAccessTitle"], parameters, options);
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
                Snackbar.Add(L["LibraryAccessUpdated"], Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
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
        var dialog = await DialogService.ShowAsync<AdminUserMediaExclusionsDialog>(L["HiddenMediaTitle"], parameters, options);
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
                Snackbar.Add(L["HiddenMediaUpdated"], Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
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
        var dialog = await DialogService.ShowAsync<AdminUserRestrictionProfileDialog>(L["RestrictionProfileTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var profileId = result.Data as Guid?;
            try
            {
                await K7ServerService.AssignContentRestrictionProfileAsync(user.Id, profileId);
                Snackbar.Add(L["RestrictionProfileUpdated"], Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
            }
        }
    }

    private async Task OpenCreateUserDialog()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateUserDialog>(L["CreateUserTitle"], options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: UserDto createdUser })
        {
            Snackbar.Add(string.Format(L["UserCreated"], createdUser.UserName), Severity.Success);
            await LoadData();
        }
    }

    private async Task OpenMergeUserDialog(UserDto user)
    {
        var parameters = new DialogParameters<MergeUserDialog>
        {
            { x => x.SourceUser, user },
            { x => x.AllUsers, _users }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<MergeUserDialog>(L["MergeUserTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["UserMerged"], Severity.Success);
            await LoadData();
        }
    }

    private async Task OpenResetPasswordDialog(UserDto user)
    {
        var parameters = new DialogParameters<ResetPasswordDialog>
        {
            { x => x.User, user }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ResetPasswordDialog>(L["ResetPasswordTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(string.Format(L["PasswordReset"], user.UserName), Severity.Success);
        }
    }
}
