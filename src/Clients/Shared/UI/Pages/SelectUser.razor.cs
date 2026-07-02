using System.Net.Http;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.ViewingGroups;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SelectUser
{
    [Inject] private ILocalUserService LocalUserService { get; set; } = default!;
    [Inject] private ICustomAuthenticationStateProvider AuthService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IViewingGroupLocalCache ViewingGroupCache { get; set; } = default!;
    [Inject] private IViewingGroupService ViewingGroupService { get; set; } = default!;
    [Inject] private IViewingGroupSessionService ViewingGroupSession { get; set; } = default!;
    [Inject] private IConnectivityService Connectivity { get; set; } = default!;

    private List<LocalUser> _users = [];
    private List<ViewingGroupDto> _groups = [];
    private bool _singleUserMode;
    private bool _loading;
    private bool _isTv;
    private bool _showOfflineButton;
    private LocalUser? _pendingUser;
    private CancellationTokenSource? _offlineTimerCts;

    protected override async Task OnInitializedAsync()
    {
        if (DeviceService.GetClientType() == ClientType.Web)
        {
            Navigation.NavigateTo("/");
            return;
        }

        _users = LocalUserService.GetAll();
        _singleUserMode = LocalUserService.IsSingleUserMode;
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        _groups = ViewingGroupCache.GetCached().ToList();

        if (Connectivity.IsOnline)
        {
            try
            {
                await ViewingGroupCache.RefreshAsync();
                _groups = ViewingGroupCache.GetCached().ToList();
            }
            catch
            {
                // Use cached groups
            }
        }

        K7.Clients.Shared.Services.AppReadySignal.Signal();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _isTv && !_loading)
        {
            await JSRuntime.InvokeVoidAsync("eval",
                "document.querySelector('.select-user-card.focusable')?.focus()");
        }
    }

    private async Task SelectUserAsync(LocalUser user)
    {
        ViewingGroupSession.ClearActiveGroup();

        if (user.PinHash is not null)
        {
            var pinValid = await PromptPinAsync(user);
            if (!pinValid)
                return;
        }

        await AuthenticateUserAsync(user);
    }

    private async Task SelectGroupAsync(ViewingGroupDto group)
    {
        if (group.HasPin)
        {
            var pinValid = await PromptGroupPinAsync(group);
            if (!pinValid)
                return;
        }

        if (string.IsNullOrEmpty(group.HostIdentityUserId))
        {
            Snackbar.Add(L["HostNotOnDevice"], K7Severity.Error);
            return;
        }

        var host = _users.FirstOrDefault(u => u.IdentityUserId == group.HostIdentityUserId);
        if (host is null)
        {
            Snackbar.Add(L["HostNotOnDevice"], K7Severity.Error);
            return;
        }

        if (host.PinHash is not null)
        {
            var pinValid = await PromptPinAsync(host);
            if (!pinValid)
                return;
        }

        _loading = true;
        _showOfflineButton = false;
        _pendingUser = host;
        StateHasChanged();
        StartOfflineTimer();

        try
        {
            var success = await AuthService.SwitchToUserAsync(host.RefreshToken);
            CancelOfflineTimer();
            if (success)
            {
                ViewingGroupSession.SetActiveGroup(group);
                LocalUserService.SetLastActiveId(host.IdentityUserId);
                Navigation.NavigateTo("/");
            }
            else
            {
                LocalUserService.Remove(host.IdentityUserId);
                _users = LocalUserService.GetAll();
                Snackbar.Add(string.Format(L["SessionExpired"], host.UserName), K7Severity.Error);
                _loading = false;
            }
        }
        catch (HttpRequestException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(host);
            ViewingGroupSession.SetActiveGroup(group);
            LocalUserService.SetLastActiveId(host.IdentityUserId);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (TaskCanceledException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(host);
            ViewingGroupSession.SetActiveGroup(group);
            LocalUserService.SetLastActiveId(host.IdentityUserId);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (Exception ex)
        {
            CancelOfflineTimer();
            Snackbar.Add(ex.Message, K7Severity.Error);
            _loading = false;
        }

        StateHasChanged();
    }

    private async Task CreateGroupAsync()
    {
        if (_users.Count == 0)
        {
            Snackbar.Add(L["AddUserFirst"], K7Severity.Warning);
            return;
        }

        var creator = _users.Count == 1
            ? _users[0]
            : await PromptCreatorUserAsync();

        if (creator is null)
            return;

        if (creator.PinHash is not null)
        {
            var pinValid = await PromptPinAsync(creator);
            if (!pinValid)
                return;
        }

        _loading = true;
        StateHasChanged();

        try
        {
            if (!await AuthService.SwitchToUserAsync(creator.RefreshToken))
            {
                Snackbar.Add(string.Format(L["SessionExpired"], creator.UserName), K7Severity.Error);
                return;
            }

            var parameters = new K7DialogParameters<CreateViewingGroupDialog>();
            var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
            var dialog = await DialogService.ShowAsync<CreateViewingGroupDialog>(L["CreateViewingGroup"], parameters, options);
            var result = await dialog.Result;

            await AuthService.LogoutAsync();

            if (result is not null && !result.Canceled)
            {
                if (Connectivity.IsOnline)
                    await ViewingGroupCache.RefreshAsync();
                _groups = ViewingGroupCache.GetCached().ToList();
                Snackbar.Add(L["GroupCreated"], K7Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            try { await AuthService.LogoutAsync(); } catch { }
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task OnGroupContextMenu(MouseEventArgs e, ViewingGroupDto group)
    {
        var edit = await DialogService.ShowMessageBoxAsync(
            group.Name,
            L["GroupManagePrompt"],
            yesText: L["EditGroup"],
            noText: L["DeleteGroup"],
            cancelText: L["CancelAction"]);

        if (edit == true)
            await EditGroupAsync(group);
        else if (edit == false)
            await DeleteGroupAsync(group);
    }

    private async Task EditGroupAsync(ViewingGroupDto group)
    {
        var creator = await AuthenticateMemberForManageAsync(group);
        if (creator is null)
            return;

        try
        {
            var parameters = new K7DialogParameters<CreateViewingGroupDialog>
            {
                { x => x.EditGroup, group }
            };
            var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
            var dialog = await DialogService.ShowAsync<CreateViewingGroupDialog>(L["EditGroup"], parameters, options);
            var result = await dialog.Result;

            await AuthService.LogoutAsync();

            if (result is not null && !result.Canceled)
            {
                if (Connectivity.IsOnline)
                    await ViewingGroupCache.RefreshAsync();
                _groups = ViewingGroupCache.GetCached().ToList();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            try { await AuthService.LogoutAsync(); } catch { }
        }
    }

    private async Task DeleteGroupAsync(ViewingGroupDto group)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteGroup"],
            string.Format(L["DeleteGroupConfirm"], group.Name),
            yesText: L["DeleteGroup"],
            cancelText: L["CancelAction"]);

        if (confirmed != true)
            return;

        var member = await AuthenticateMemberForManageAsync(group);
        if (member is null)
            return;

        try
        {
            await ViewingGroupService.DeleteAsync(group.Id);
            await AuthService.LogoutAsync();
            if (Connectivity.IsOnline)
                await ViewingGroupCache.RefreshAsync();
            _groups = ViewingGroupCache.GetCached().ToList();
            Snackbar.Add(L["GroupDeleted"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            try { await AuthService.LogoutAsync(); } catch { }
        }
    }

    private async Task<LocalUser?> AuthenticateMemberForManageAsync(ViewingGroupDto group)
    {
        var memberIdentityIds = group.Members
            .Select(m => m.IdentityUserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet();

        var localMembers = _users.Where(u => memberIdentityIds.Contains(u.IdentityUserId)).ToList();
        if (localMembers.Count == 0)
        {
            Snackbar.Add(L["MemberNotOnDevice"], K7Severity.Error);
            return null;
        }

        var user = localMembers.Count == 1 ? localMembers[0] : await PromptCreatorUserAsync(localMembers);
        if (user is null)
            return null;

        if (user.PinHash is not null)
        {
            var pinValid = await PromptPinAsync(user);
            if (!pinValid)
                return null;
        }

        if (!await AuthService.SwitchToUserAsync(user.RefreshToken))
        {
            Snackbar.Add(string.Format(L["SessionExpired"], user.UserName), K7Severity.Error);
            return null;
        }

        return user;
    }

    private async Task AuthenticateUserAsync(LocalUser user)
    {
        _loading = true;
        _showOfflineButton = false;
        _pendingUser = user;
        StateHasChanged();

        StartOfflineTimer();

        try
        {
            var success = await AuthService.SwitchToUserAsync(user.RefreshToken);
            CancelOfflineTimer();
            if (success)
            {
                LocalUserService.SetLastActiveId(user.IdentityUserId);
                Navigation.NavigateTo("/");
            }
            else
            {
                LocalUserService.Remove(user.IdentityUserId);
                _users = LocalUserService.GetAll();
                Snackbar.Add(string.Format(L["SessionExpired"], user.UserName), K7Severity.Error);
                _loading = false;
            }
        }
        catch (HttpRequestException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(user);
            LocalUserService.SetLastActiveId(user.IdentityUserId);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (TaskCanceledException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(user);
            LocalUserService.SetLastActiveId(user.IdentityUserId);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (Exception ex)
        {
            CancelOfflineTimer();
            Snackbar.Add(ex.Message, K7Severity.Error);
            _loading = false;
        }

        StateHasChanged();
    }

    private void StartOfflineTimer()
    {
        _offlineTimerCts?.Cancel();
        _offlineTimerCts = new CancellationTokenSource();
        var token = _offlineTimerCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            if (!token.IsCancellationRequested)
            {
                await InvokeAsync(() =>
                {
                    _showOfflineButton = true;
                    StateHasChanged();
                });
            }
        }, token);
    }

    private void CancelOfflineTimer()
    {
        _offlineTimerCts?.Cancel();
        _offlineTimerCts?.Dispose();
        _offlineTimerCts = null;
    }

    private Task ContinueOfflineAsync()
    {
        if (_pendingUser is null)
            return Task.CompletedTask;

        CancelOfflineTimer();
        AuthService.SignInOffline(_pendingUser);
        LocalUserService.SetLastActiveId(_pendingUser.IdentityUserId);
        Navigation.NavigateTo("/");
        return Task.CompletedTask;
    }

    private async Task<bool> PromptPinAsync(LocalUser user)
    {
        var parameters = new K7DialogParameters<PinDialog>
        {
            { x => x.UserName, user.UserName }
        };
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<PinDialog>(L["EnterPin"], parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string pin)
            return false;

        if (!LocalUserService.VerifyPin(user.IdentityUserId, pin))
        {
            Snackbar.Add(L["IncorrectPin"], K7Severity.Error);
            return false;
        }

        return true;
    }

    private async Task<bool> PromptGroupPinAsync(ViewingGroupDto group)
    {
        var parameters = new K7DialogParameters<PinDialog>
        {
            { x => x.UserName, group.Name }
        };
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<PinDialog>(L["EnterGroupPin"], parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string pin)
            return false;

        if (!ViewingGroupService.VerifyGroupPin(group, pin))
        {
            Snackbar.Add(L["IncorrectPin"], K7Severity.Error);
            return false;
        }

        return true;
    }

    private async Task<LocalUser?> PromptCreatorUserAsync(IReadOnlyList<LocalUser>? candidates = null)
    {
        candidates ??= _users;
        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        var parameters = new K7DialogParameters<SelectLocalUserDialog>
        {
            { x => x.Users, candidates.ToList() }
        };
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<SelectLocalUserDialog>(L["SelectCreator"], parameters, options);
        var result = await dialog.Result;

        return result is not null && !result.Canceled && result.Data is LocalUser user ? user : null;
    }

    private async Task AddUserAsync()
    {
        if (_isTv)
        {
            Navigation.NavigateTo("/linkdevice");
            return;
        }

        _loading = true;
        StateHasChanged();

        try
        {
            await AuthService.LoginAsync();

            var authState = await ((AuthenticationStateProvider)AuthService).GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                Navigation.NavigateTo("/");
            }
            else
            {
                _loading = false;
                _users = LocalUserService.GetAll();
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            _loading = false;
            StateHasChanged();
        }
    }

    private void OnSingleUserModeChanged(bool value)
    {
        _singleUserMode = value;
        LocalUserService.IsSingleUserMode = value;
    }

    private async Task OnUserKeyDown(KeyboardEventArgs e, LocalUser user)
    {
        if (e.Key is "Enter" or " ")
            await SelectUserAsync(user);
    }

    private async Task OnGroupKeyDown(KeyboardEventArgs e, ViewingGroupDto group)
    {
        if (e.Key is "Enter" or " ")
            await SelectGroupAsync(group);
    }

    private async Task OnCreateGroupKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " ")
            await CreateGroupAsync();
    }

    private async Task OnAddUserKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " ")
            await AddUserAsync();
    }

    private static string GetInitial(LocalUser user)
    {
        var name = user.DisplayName ?? user.UserName;
        return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
    }

    private static string GetMemberInitial(ViewingGroupMemberDto member)
    {
        var name = member.DisplayName;
        return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
    }
}
