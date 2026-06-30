using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

using K7.Clients.Shared.UI.Pages.Admin.Dialogs;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminUsersPanel : IDisposable
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    private bool _isLoading = true;
    private List<UserDto> _users = [];
    private K7DataTable<UserDto>? _tableRef;
    private Guid? _currentUserId;
    private Guid? _highlightedUserId;
    private bool _shouldScrollToHighlighted;
    private HashSet<string> _onlineIdentityUserIds = new(StringComparer.Ordinal);

    protected override async Task OnInitializedAsync()
    {
        HubClient.OnlineUsersPresenceUpdated += OnOnlineUsersPresenceUpdated;
        HubClient.ConnectionStateChanged += OnHubConnectionStateChanged;

        await LoadData();
        ParseFragment();
        await JoinPresenceGroupAsync();
    }

    public void Dispose()
    {
        HubClient.OnlineUsersPresenceUpdated -= OnOnlineUsersPresenceUpdated;
        HubClient.ConnectionStateChanged -= OnHubConnectionStateChanged;
    }

    private async Task JoinPresenceGroupAsync()
    {
        try
        {
            await HubClient.JoinAdminStreamsGroupAsync();
        }
        catch
        {
        }
    }

    private void OnHubConnectionStateChanged(HubConnectionState state)
    {
        if (state == HubConnectionState.Connected)
            _ = JoinPresenceGroupAsync();
    }

    private void OnOnlineUsersPresenceUpdated(OnlineUsersPresenceDto presence)
    {
        _onlineIdentityUserIds = presence.IdentityUserIds.ToHashSet(StringComparer.Ordinal);
        InvokeAsync(StateHasChanged);
    }

    private bool IsUserOnline(UserDto user) =>
        !string.IsNullOrEmpty(user.IdentityUserId) && _onlineIdentityUserIds.Contains(user.IdentityUserId);

    private string GetPresenceBadgeClass(UserDto user) =>
        IsUserOnline(user) ? "connected" : "offline";

    private string GetPresenceTitle(UserDto user) =>
        IsUserOnline(user) ? L["UserOnline"] : L["UserOffline"];

    private bool IsCurrentUser(UserDto user) =>
        _currentUserId is not null && user.Id == _currentUserId;

    private string? GetUserRowClass(UserDto user) => GetUserHighlightClass(user);

    private string GetUserCardClass(UserDto user) => GetUserHighlightClass(user) ?? string.Empty;

    private string? GetUserHighlightClass(UserDto user)
    {
        var isCurrent = IsCurrentUser(user);
        var isHighlighted = user.Id == _highlightedUserId;
        return (isCurrent, isHighlighted) switch
        {
            (true, true) => "current-user row-highlighted",
            (true, false) => "current-user",
            (false, true) => "row-highlighted",
            _ => null
        };
    }

    private void NavigateToPlaybackHistory(UserDto user) =>
        NavigationManager.NavigateTo($"/admin/playback-history?userId={user.Id}");

    private void OnColumnPickerClick() => _tableRef?.ToggleColumnPicker();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldScrollToHighlighted && _highlightedUserId is not null)
        {
            _shouldScrollToHighlighted = false;
            await JSRuntime.InvokeVoidAsync("K7.scrollToElement", $"user-{_highlightedUserId}");
        }
    }

    private void ParseFragment()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var fragment = uri.Fragment.TrimStart('#');
        if (Guid.TryParse(fragment, out var userId) && _users.Any(u => u.Id == userId))
        {
            _highlightedUserId = userId;
            _shouldScrollToHighlighted = true;
        }
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
            Snackbar.Add(string.Format(L["RoleUpdated"], user.UserName), K7Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OnToggleActive(UserDto user, bool isActive)
    {
        try
        {
            await K7ServerService.ToggleUserActiveAsync(user.Id, isActive);
            var label = user.UserName ?? user.Email;
            Snackbar.Add(isActive ? string.Format(L["UserActivated"], label) : string.Format(L["UserDeactivated"], label), K7Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OpenCapabilitiesDialog(UserDto user)
    {
        var allCapabilities = Enum.GetValues<Capability>();
        var defaultCaps = DefaultCapabilities.ForRole(user.Role);

        var overrides = user.CapabilityOverrides
            .ToDictionary(o => o.Capability, o => o.Enabled);

        var parameters = new K7DialogParameters<AdminUserCapabilitiesDialog>
        {
            { x => x.UserName, user.UserName ?? user.Email ?? "" },
            { x => x.Role, user.Role },
            { x => x.Overrides, overrides }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
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
                Snackbar.Add(L["CapabilitiesUpdated"], K7Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }

    private async Task ConfirmDelete(UserDto user)
    {
        var parameters = new K7DialogParameters<ConfirmDeleteUserDialog>
        {
            { x => x.DisplayName, user.UserName ?? user.Email ?? L["DeleteFallbackName"] }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteUserDialog>(L["DeleteUserTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await K7ServerService.DeleteUserAsync(user.Id);
                Snackbar.Add(L["UserDeleted"], K7Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }

    private async Task OpenLibraryExclusionsDialog(UserDto user)
    {
        var parameters = new K7DialogParameters<AdminUserLibraryExclusionsDialog>
        {
            { x => x.ExcludedLibraryIds, user.LibraryExclusions.Where(e => e.IsAdminExcluded).Select(e => e.LibraryId).ToList() }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
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
                Snackbar.Add(L["LibraryAccessUpdated"], K7Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }

    private async Task OpenMediaExclusionsDialog(UserDto user)
    {
        var parameters = new K7DialogParameters<AdminUserMediaExclusionsDialog>
        {
            { x => x.ExcludedMediaIds, user.MediaExclusions.Where(e => e.IsAdminExcluded).Select(e => e.MediaId).ToList() }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
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
                Snackbar.Add(L["HiddenMediaUpdated"], K7Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }

    private async Task OpenRestrictionProfileDialog(UserDto user)
    {
        var parameters = new K7DialogParameters<AdminUserRestrictionProfileDialog>
        {
            { x => x.CurrentProfileId, user.ContentRestrictionProfileId }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AdminUserRestrictionProfileDialog>(L["RestrictionProfileTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var profileId = result.Data as Guid?;
            try
            {
                await K7ServerService.AssignContentRestrictionProfileAsync(user.Id, profileId);
                Snackbar.Add(L["RestrictionProfileUpdated"], K7Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }

    private async Task OpenCreateUserDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateUserDialog>(L["CreateUserTitle"], null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: UserDto createdUser })
        {
            Snackbar.Add(string.Format(L["UserCreated"], createdUser.UserName), K7Severity.Success);
            await LoadData();
        }
    }

    private async Task OpenMergeUserDialog(UserDto user)
    {
        var parameters = new K7DialogParameters<MergeUserDialog>
        {
            { x => x.SourceUser, user },
            { x => x.AllUsers, _users }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<MergeUserDialog>(L["MergeUserTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["UserMerged"], K7Severity.Success);
            await LoadData();
        }
    }

    private async Task OpenResetPasswordDialog(UserDto user)
    {
        var parameters = new K7DialogParameters<ResetPasswordDialog>
        {
            { x => x.User, user }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ResetPasswordDialog>(L["ResetPasswordTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(string.Format(L["PasswordReset"], user.UserName), K7Severity.Success);
        }
    }
}
