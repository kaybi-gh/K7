using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Shared.Dtos;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsWatchTogetherSharedProfilesPage
{
    [Inject] private ISharedProfileService SharedProfileService { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private ISharedProfileDevicePinService SharedProfileDevicePin { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SettingsSharedProfilesPage> L { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private bool _loading = true;
    private bool _savingPreference;
    private bool _dialogOpen;
    private Guid? _currentUserId;
    private SharedProfilePreferencesDto _preferences = new();
    private List<SharedProfileDto> _groups = [];
    private HashSet<Guid> _processing = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var me = await UserAdminService.GetCurrentUserAsync();
            _currentUserId = me?.Id;

            _preferences = await PreferencesService.GetSharedProfilePreferencesAsync();
            await ReloadGroupsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ReloadGroupsAsync()
    {
        _groups = (await SharedProfileService.GetSharedProfilesAsync()).ToList();
    }

    private void OnShowOnDeviceChanged(SharedProfileDto group, bool value) =>
        SharedProfileDevicePin.SetPinned(group.Id, value);

    private async Task OnBlockNewMembershipChanged(bool value)
    {
        _preferences.BlockNewMembership = value;
        _savingPreference = true;
        try
        {
            await PreferencesService.UpdateSharedProfilePreferencesAsync(_preferences);
            Snackbar.Add(S["Saved"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            _preferences.BlockNewMembership = !value;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _savingPreference = false;
        }
    }

    private async Task CreateGroupAsync()
    {
        _dialogOpen = true;
        try
        {
            var parameters = new K7DialogParameters<CreateSharedProfileDialog>();
            var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
            var dialog = await DialogService.ShowAsync<CreateSharedProfileDialog>(L["CreateGroup"], parameters, options);
            var result = await dialog.Result;

            if (result is not null && !result.Canceled)
            {
                await ReloadGroupsAsync();
                Snackbar.Add(L["GroupCreated"], K7Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async Task EditGroupAsync(SharedProfileDto group)
    {
        _dialogOpen = true;
        try
        {
            var parameters = new K7DialogParameters<CreateSharedProfileDialog>
            {
                { x => x.EditGroup, group }
            };
            var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
            var dialog = await DialogService.ShowAsync<CreateSharedProfileDialog>(L["EditGroup"], parameters, options);
            var result = await dialog.Result;

            if (result is not null && !result.Canceled)
            {
                await ReloadGroupsAsync();
                Snackbar.Add(L["GroupUpdated"], K7Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async Task EditProfileSettingsAsync(SharedProfileDto group)
    {
        _dialogOpen = true;
        try
        {
            var parameters = new K7DialogParameters<SharedProfileHostSettingsDialog>
            {
                { x => x.Group, group }
            };
            var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
            var dialog = await DialogService.ShowAsync<SharedProfileHostSettingsDialog>(L["ProfileSettings"], parameters, options);
            var result = await dialog.Result;
            if (result is not null && !result.Canceled)
            {
                await ReloadGroupsAsync();
                Snackbar.Add(S["Saved"], K7Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async Task LeaveAsync(SharedProfileDto group)
    {
        if (_currentUserId is null)
            return;

        _processing.Add(group.Id);
        try
        {
            var isHost = group.HostUserId == _currentUserId;
            var otherMembers = group.Members.Where(m => m.UserId != _currentUserId).ToList();

            if (group.Members.Count <= SharedProfileMemberValidatorMinMembers)
            {
                var confirmed = await DialogService.ShowMessageBoxAsync(
                    L["LeaveTitle"],
                    L["LeaveDeletesGroup"],
                    yesText: L["Leave"],
                    cancelText: S["Cancel"]);
                if (confirmed != true)
                    return;

                await SharedProfileService.LeaveAsync(group.Id);
            }
            else if (isHost)
            {
                var parameters = new K7DialogParameters<LeaveSharedProfileDialog>
                {
                    { x => x.OtherMembers, otherMembers }
                };
                var dialog = await DialogService.ShowAsync<LeaveSharedProfileDialog>(
                    L["TransferHostTitle"],
                    parameters,
                    new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true });
                var result = await dialog.Result;
                if (result is null || result.Canceled || result.Data is not Guid newHostUserId)
                    return;

                await SharedProfileService.LeaveAsync(group.Id, newHostUserId);
            }
            else
            {
                var confirmed = await DialogService.ShowMessageBoxAsync(
                    L["LeaveTitle"],
                    string.Format(L["LeaveConfirm"], group.Name),
                    yesText: L["Leave"],
                    cancelText: S["Cancel"]);
                if (confirmed != true)
                    return;

                await SharedProfileService.LeaveAsync(group.Id);
            }

            _groups.RemoveAll(g => g.Id == group.Id);
            SharedProfileDevicePin.SetPinned(group.Id, false);
            Snackbar.Add(L["LeftGroup"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _processing.Remove(group.Id);
        }
    }

    private const int SharedProfileMemberValidatorMinMembers = 2;
}
