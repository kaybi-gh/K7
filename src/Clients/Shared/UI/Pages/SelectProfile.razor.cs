using System.Net.Http;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Extensions;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;
using K7.Shared.Dtos.SharedProfiles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SelectProfile
{
    private const string ProfileSelectKindUser = "user";
    private const string ProfileSelectKindGroup = "group";

    [Inject] private ILocalUserService LocalUserService { get; set; } = default!;
    [Inject] private ICustomAuthenticationStateProvider AuthService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private ISharedProfileLocalCache SharedProfileCache { get; set; } = default!;
    [Inject] private ISharedProfileService SharedProfileService { get; set; } = default!;
    [Inject] private ISharedProfileSessionService SharedProfileSession { get; set; } = default!;
    [Inject] private ISharedProfileDevicePinService SharedProfileDevicePin { get; set; } = default!;
    [Inject] private IConnectivityService Connectivity { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IDeviceStorageService Storage { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    private List<LocalUser> _users = [];
    private List<SharedProfileDto> _pinnedGroups = [];
    private bool _singleUserMode;
    private bool _loading;
    private bool _isTv;
    private DeviceType _deviceType;
    private bool _showOfflineButton;
    private LocalUser? _pendingUser;
    private CancellationTokenSource? _offlineTimerCts;
    private Carousel? _carousel;
    private string? _initialFocusUserId;
    private Guid? _initialFocusGroupId;
    private bool _initialFocusAddCard;
    private int _initialFocusCarouselIndex;
    private bool _profileFocusApplied;
    private bool _selecting;

    private static IReadOnlyDictionary<string, object>? GetInitialFocusAttributes(bool isTarget) =>
        isTarget ? new Dictionary<string, object> { ["data-initial-focus"] = true } : null;

    protected override async Task OnInitializedAsync()
    {
        if (DeviceService.GetClientType() == ClientType.Web)
        {
            Navigation.NavigateTo("/");
            return;
        }

        _users = LocalUserService.GetAll();
        _singleUserMode = LocalUserService.IsSingleUserMode;
        _deviceType = await DeviceService.GetDeviceTypeAsync();
        _isTv = _deviceType == DeviceType.TV;

        // Cold start can reach select-profile even when single-user mode is on (e.g. last-active
        // was never written, or session restore ran before SecureStorage was ready). Retry once
        // here - but only when still anonymous so "Switch user" keeps working.
        // PIN-protected profiles require a prior unlock (PIN entered) before auto-auth.
        if (_singleUserMode && _users.Count > 0)
        {
            var authState = await ((AuthenticationStateProvider)AuthService).GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated != true)
            {
                var autoUser = ResolveSoloAutoUser();
                if (autoUser is not null)
                {
                    K7.Clients.Shared.Services.AppReadySignal.Signal();
                    await AuthenticateUserAsync(autoUser);
                    return;
                }
            }
        }

        if (Connectivity.IsOnline)
        {
            try
            {
                await SharedProfileCache.RefreshAsync();
            }
            catch
            {
                // Use cached groups
            }
        }

        ReloadPinnedGroups();
        ComputeInitialFocusTarget();
        K7.Clients.Shared.Services.AppReadySignal.Signal();
    }

    private void ReloadPinnedGroups()
    {
        var pinnedIds = SharedProfileDevicePin.GetPinnedGroupIds();
        _pinnedGroups = SharedProfileCache.GetCached()
            .Where(g => pinnedIds.Contains(g.Id))
            .ToList();
    }

    private void ComputeInitialFocusTarget()
    {
        _initialFocusUserId = null;
        _initialFocusGroupId = null;
        _initialFocusAddCard = false;
        _initialFocusCarouselIndex = 0;

        if (!TryResolveStoredFocusTarget())
            FocusFirstCarouselItem();
    }

    private bool TryResolveStoredFocusTarget()
    {
        if (Storage.Get(PreferenceKeys.LAST_PROFILE_SELECT_AT) <= 0)
            return false;

        var kind = Storage.Get(PreferenceKeys.LAST_PROFILE_SELECT_KIND);
        var id = Storage.Get(PreferenceKeys.LAST_PROFILE_SELECT_ID);
        if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id))
            return false;

        if (kind == ProfileSelectKindGroup && Guid.TryParse(id, out var groupId))
        {
            var groupIndex = _pinnedGroups.FindIndex(g => g.Id == groupId);
            if (groupIndex < 0)
                return false;

            _initialFocusGroupId = groupId;
            _initialFocusCarouselIndex = _users.Count + groupIndex;
            return true;
        }

        if (kind == ProfileSelectKindUser)
        {
            var userIndex = _users.FindIndex(u => u.IdentityUserId == id);
            if (userIndex < 0)
                return false;

            _initialFocusUserId = id;
            _initialFocusCarouselIndex = userIndex;
            return true;
        }

        return false;
    }

    private void FocusFirstCarouselItem()
    {
        if (_users.Count > 0)
        {
            _initialFocusUserId = _users[0].IdentityUserId;
            return;
        }

        if (_pinnedGroups.Count > 0)
        {
            _initialFocusGroupId = _pinnedGroups[0].Id;
            return;
        }

        _initialFocusAddCard = true;
    }

    private void RecordUserSelection(string identityUserId)
    {
        Storage.Set(PreferenceKeys.LAST_PROFILE_SELECT_KIND, ProfileSelectKindUser);
        Storage.Set(PreferenceKeys.LAST_PROFILE_SELECT_ID, identityUserId);
        Storage.Set(PreferenceKeys.LAST_PROFILE_SELECT_AT, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private void RecordSharedProfileSelection(Guid groupId)
    {
        Storage.Set(PreferenceKeys.LAST_PROFILE_SELECT_KIND, ProfileSelectKindGroup);
        Storage.Set(PreferenceKeys.LAST_PROFILE_SELECT_ID, groupId.ToString());
        Storage.Set(PreferenceKeys.LAST_PROFILE_SELECT_AT, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private async Task ApplyInitialTvFocusAsync()
    {
        if (_carousel is not null)
        {
            await _carousel.EnsureInitializedAsync();

            if (_initialFocusCarouselIndex > 0)
            {
                var itemCount = _users.Count + _pinnedGroups.Count + 1;
                if (_initialFocusCarouselIndex < itemCount)
                    await _carousel.ScrollToIndexAsync(_initialFocusCarouselIndex);
            }
        }

        await SpatialNav.RefreshAsync();
        await SpatialNav.FocusFirstAsync("[data-initial-focus]");
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isTv || _loading || _profileFocusApplied)
            return;

        try
        {
            await ApplyInitialTvFocusAsync();
            _profileFocusApplied = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ShowCarousel()
    {
        _loading = false;
        _profileFocusApplied = false;
    }

    private async Task SelectUserAsync(LocalUser user)
    {
        if (_selecting)
            return;

        _selecting = true;
        try
        {
            SharedProfileSession.ClearActiveGroup();

            if (user.HasPin)
            {
                var pinValid = await PromptPinAsync(user);
                if (!pinValid)
                    return;
            }

            await AuthenticateUserAsync(user);
        }
        finally
        {
            _selecting = false;
        }
    }

    private async Task SelectGroupAsync(SharedProfileDto group)
    {
        if (_selecting)
            return;

        _selecting = true;
        try
        {
            await SelectGroupCoreAsync(group);
        }
        finally
        {
            _selecting = false;
        }
    }

    private async Task SelectGroupCoreAsync(SharedProfileDto group)
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

        if (host.HasPin)
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
            var success = await AuthService.SwitchToUserAsync(host.IdentityUserId);
            CancelOfflineTimer();
            if (success)
            {
                SharedProfileSession.SetActiveGroup(group);
                LocalUserService.SetLastActiveId(host.IdentityUserId);
                RecordSharedProfileSelection(group.Id);
                Navigation.NavigateTo("/");
            }
            else
            {
                LocalUserService.Remove(host.IdentityUserId);
                _users = LocalUserService.GetAll();
                Snackbar.Add(string.Format(L["SessionExpired"], host.UserName), K7Severity.Error);
                ShowCarousel();
            }
        }
        catch (HttpRequestException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(host);
            SharedProfileSession.SetActiveGroup(group);
            LocalUserService.SetLastActiveId(host.IdentityUserId);
            RecordSharedProfileSelection(group.Id);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (TaskCanceledException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(host);
            SharedProfileSession.SetActiveGroup(group);
            LocalUserService.SetLastActiveId(host.IdentityUserId);
            RecordSharedProfileSelection(group.Id);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (Exception ex)
        {
            CancelOfflineTimer();
            Snackbar.Add(ex.Message, K7Severity.Error);
            ShowCarousel();
        }

        StateHasChanged();
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
            var success = await AuthService.SwitchToUserAsync(user.IdentityUserId);
            CancelOfflineTimer();
            if (success)
            {
                LocalUserService.SetLastActiveId(user.IdentityUserId);
                MarkSoloUnlockedIfNeeded(user);
                RecordUserSelection(user.IdentityUserId);
                Navigation.NavigateTo("/");
            }
            else
            {
                LocalUserService.Remove(user.IdentityUserId);
                _users = LocalUserService.GetAll();
                Snackbar.Add(string.Format(L["SessionExpired"], user.UserName), K7Severity.Error);
                ShowCarousel();
            }
        }
        catch (HttpRequestException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(user);
            LocalUserService.SetLastActiveId(user.IdentityUserId);
            MarkSoloUnlockedIfNeeded(user);
            RecordUserSelection(user.IdentityUserId);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (TaskCanceledException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(user);
            LocalUserService.SetLastActiveId(user.IdentityUserId);
            MarkSoloUnlockedIfNeeded(user);
            RecordUserSelection(user.IdentityUserId);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (Exception ex)
        {
            CancelOfflineTimer();
            Snackbar.Add(ex.Message, K7Severity.Error);
            ShowCarousel();
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
        MarkSoloUnlockedIfNeeded(_pendingUser);
        Navigation.NavigateTo("/");
        return Task.CompletedTask;
    }

    private async Task<bool> PromptPinAsync(LocalUser user)
    {
        var parameters = new K7DialogParameters<PinDialog>
        {
            { x => x.UserName, user.UserName }
        };
        var options = K7DialogServiceExtensions.CreatePinDialogOptions(_deviceType);
        var dialog = await DialogService.ShowAsync<PinDialog>(L["EnterPin"], parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string pin)
            return false;

        if (!await VerifyUserPinAsync(user, pin))
        {
            Snackbar.Add(L["IncorrectPin"], K7Severity.Error);
            return false;
        }

        return true;
    }

    private async Task<bool> VerifyUserPinAsync(LocalUser user, string pin)
    {
        if (Connectivity.IsOnline && user.UserId is { } userId)
        {
            try
            {
                return await UserAdminService.VerifyUserPinAsync(userId, pin);
            }
            catch
            {
                // Fall back to locally cached PIN verification when offline.
            }
        }

        return LocalUserService.VerifyPin(user.IdentityUserId, pin);
    }

    private async Task<bool> PromptGroupPinAsync(SharedProfileDto group)
    {
        var parameters = new K7DialogParameters<PinDialog>
        {
            { x => x.UserName, group.Name }
        };
        var options = K7DialogServiceExtensions.CreatePinDialogOptions(_deviceType);
        var dialog = await DialogService.ShowAsync<PinDialog>(L["EnterGroupPin"], parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string pin)
            return false;

        if (!await SharedProfileService.VerifyGroupPinAsync(group, pin))
        {
            Snackbar.Add(L["IncorrectPin"], K7Severity.Error);
            return false;
        }

        return true;
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
                if (LocalUserService.IsSingleUserMode)
                {
                    var last = LocalUserService.GetLastActive();
                    if (last is not null)
                        LocalUserService.MarkSingleUserUnlocked(last.IdentityUserId);
                }
            }
            else
            {
                ShowCarousel();
                _users = LocalUserService.GetAll();
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            ShowCarousel();
            StateHasChanged();
        }
    }

    private void OnSingleUserModeChanged(bool value)
    {
        _singleUserMode = value;
        LocalUserService.IsSingleUserMode = value;

        if (!value)
        {
            // Drop solo restore state so a later re-enable cannot skip PIN / profile pick
            // without successfully entering a user again while solo is on.
            LocalUserService.ClearSingleUserUnlocked();
            LocalUserService.ClearLastActiveId();
            return;
        }

        // Checking the box alone does not arm auto-login. That happens only after a
        // successful profile entry (PIN if any) while solo mode is enabled.
    }

    /// <summary>
    /// Solo auto-login only after checkbox + successful entry (LastActive and unlock).
    /// </summary>
    private LocalUser? ResolveSoloAutoUser()
    {
        var candidate = LocalUserService.GetLastActive();
        if (candidate is null)
            return null;

        if (!LocalUserService.IsSingleUserUnlocked(candidate.IdentityUserId))
            return null;

        return candidate;
    }

    private void MarkSoloUnlockedIfNeeded(LocalUser user)
    {
        if (LocalUserService.IsSingleUserMode)
            LocalUserService.MarkSingleUserUnlocked(user.IdentityUserId);
    }

    private static string GetInitial(LocalUser user)
    {
        var name = user.DisplayName ?? user.UserName;
        return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
    }

    private static string GetMemberInitial(SharedProfileMemberDto member)
    {
        var name = member.DisplayName;
        return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
    }

    private static K7AvatarGroupItem ToAvatarGroupItem(SharedProfileMemberDto member) =>
        new()
        {
            UserId = member.UserId,
            Image = member.AvatarUrl,
            Letter = GetMemberInitial(member)
        };
}
