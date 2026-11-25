using System.Net.Http;
using System.Text.Json;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Extensions;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;
using K7.Shared.Dtos.SharedProfiles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SelectProfile : IAsyncDisposable
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
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private readonly record struct PickerEntry(string Token, LocalUser? User, SharedProfileDto? Group);

    private List<LocalUser> _users = [];
    private List<SharedProfileDto> _pinnedGroups = [];
    private List<PickerEntry> _entries = [];
    private List<PickerEntry>? _editBaseline;
    private HashSet<string> _hiddenUserIds = new(StringComparer.Ordinal);
    private HashSet<string>? _hiddenBaseline;
    private bool _editing;
    private SelectionModeKeyboardBinder? _editBackBinder;
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
    /// <summary>
    /// False on first paint (shell only). Carousel/avatars mount after AppReady so EmptyLayout
    /// is not blocked by component JIT on Android TV (~15s).
    /// </summary>
    private bool _paintPicker;

    private static IReadOnlyDictionary<string, object>? GetInitialFocusAttributes(bool isTarget) =>
        isTarget ? new Dictionary<string, object> { ["data-initial-focus"] = true } : null;

    protected override void OnParametersSet()
    {
        // Just before the first render tree build (init completes synchronously on MAUI).
        ColdStartTrace.Write("SelectProfile-params-set");
    }

    protected override async Task OnInitializedAsync()
    {
        ColdStartTrace.Write("SelectProfile-init-start");
        if (DeviceService.GetClientType() == ClientType.Web)
        {
            Navigation.NavigateTo("/");
            return;
        }

        ReloadLocalUsers();
        _singleUserMode = LocalUserService.IsSingleUserMode;
        // Same path as local users: device storage only, before the first await,
        // so a pinned shared profile is in the first paint.
        ReloadPinnedGroups();
        ComputeInitialFocusTarget();
        // Prefer cache: await GetDeviceTypeAsync is cheap on MAUI but still yields a frame.
        _deviceType = DeviceService.CachedDeviceType ?? await DeviceService.GetDeviceTypeAsync();
        _isTv = _deviceType == DeviceType.TV;

        // Cold start can reach select-profile even when single-user mode is on (e.g. last-active
        // was never written, or session restore ran before SecureStorage was ready). Retry once
        // here - but only when still anonymous so "Switch user" keeps working.
        // PIN-protected profiles require a prior unlock (PIN entered) before auto-auth.
        // Do not await a long OpenIddict restore here: GetAuthenticationStateAsync returns
        // current state immediately; restore notifies when finished.
        if (_singleUserMode && _users.Count > 0)
        {
            var authState = await ((AuthenticationStateProvider)AuthService).GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated != true)
            {
                var autoUser = ResolveSoloAutoUser();
                if (autoUser is not null)
                {
                    // Dead refresh token: show the picker (reconnect badge), never open the
                    // system browser under a spinner - that left users stuck after snackbar.
                    if (NeedsReconnect(autoUser))
                    {
                        Snackbar.Add(string.Format(L["SessionExpired"], autoUser.UserName), K7Severity.Error);
                        await SignalAppReadyAfterInitAsync();
                        return;
                    }

                    await AuthenticateUserAsync(autoUser, allowInteractiveReconnect: false);
                    await SignalAppReadyAfterInitAsync();
                    return;
                }
            }
        }

        await SignalAppReadyAfterInitAsync();
    }

    /// <summary>
    /// Do not wait for EmptyLayout OnAfterRender (can lag ~10s after init on TV). Splash is
    /// already dismissed from WebView init. Never await JS here: InvokeAsync before first
    /// render blocked ~13s on TV (interop waits for a painted circuit).
    /// </summary>
    private Task SignalAppReadyAfterInitAsync()
    {
        ColdStartTrace.Write("SelectProfile-init-end");
        AppReadySignal.Signal();
        // EmptyLayout still calls K7.dismissPreload on first paint.
        return Task.CompletedTask;
    }

    private void ReloadPinnedGroups()
    {
        var pinnedIds = SharedProfileDevicePin.GetPinnedGroupIds();
        _pinnedGroups = SharedProfileCache.GetCached()
            .Where(g => pinnedIds.Contains(g.Id))
            .ToList();
        RebuildEntries();
    }

    private void ReloadLocalUsers()
    {
        _users = LocalUserService.GetAll();
        if (!_editing)
            _hiddenUserIds = ProfilePickerHiddenUsers.Read(Storage);

        RebuildEntries();
    }

    private void RebuildEntries()
    {
        if (_editing)
            return;

        _entries = BuildEntries(includeHidden: false);
    }

    private List<PickerEntry> BuildEntries(bool includeHidden)
    {
        var raw = new List<PickerEntry>(_users.Count + _pinnedGroups.Count);
        foreach (var user in _users)
        {
            if (!includeHidden && _hiddenUserIds.Contains(user.IdentityUserId))
                continue;

            raw.Add(new PickerEntry(ProfilePickerOrder.User(user.IdentityUserId), user, null));
        }

        foreach (var group in _pinnedGroups)
            raw.Add(new PickerEntry(ProfilePickerOrder.Group(group.Id), null, group));

        return ProfilePickerOrder.Arrange(raw, entry => entry.Token, ReadOrder());
    }

    private bool IsUserHidden(LocalUser user) => _hiddenUserIds.Contains(user.IdentityUserId);

    private void ToggleHidden(LocalUser user)
    {
        if (!_hiddenUserIds.Remove(user.IdentityUserId))
            _hiddenUserIds.Add(user.IdentityUserId);
    }

    private List<string> ReadOrder()
    {
        var json = Storage.Get(PreferenceKeys.PROFILE_PICKER_ORDER);
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveOrder()
    {
        var tokens = _entries.Select(entry => entry.Token).ToList();
        Storage.Set(PreferenceKeys.PROFILE_PICKER_ORDER, JsonSerializer.Serialize(tokens));
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
            var groupIndex = _entries.FindIndex(entry => entry.Group?.Id == groupId);
            if (groupIndex < 0)
                return false;

            _initialFocusGroupId = groupId;
            _initialFocusCarouselIndex = groupIndex;
            return true;
        }

        if (kind == ProfileSelectKindUser)
        {
            var userIndex = _entries.FindIndex(entry => entry.User?.IdentityUserId == id);
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
        if (_entries.Count > 0)
        {
            var first = _entries[0];
            if (first.User is not null)
                _initialFocusUserId = first.User.IdentityUserId;
            else if (first.Group is not null)
                _initialFocusGroupId = first.Group.Id;

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
                var trailing = _editing ? 2 : _entries.Count > 0 ? 2 : 1;
                var itemCount = _entries.Count + trailing;
                if (_initialFocusCarouselIndex < itemCount)
                    await _carousel.ScrollToIndexAsync(_initialFocusCarouselIndex);
            }
        }

        await SpatialNav.RefreshAsync();
        await SpatialNav.FocusFirstAsync("[data-initial-focus]");
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_paintPicker)
        {
            ColdStartTrace.Write("SelectProfile-shell-painted");
            // Mount rich picker after this frame so EmptyLayout can Signal AppReady first.
            _paintPicker = true;
            _ = InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        }

        // Do not await carousel JS / Embla / spatial nav here. EmptyLayout AppReadySignal
        // runs after child OnAfterRenderAsync.
        if (!_paintPicker || !_isTv || _loading || _profileFocusApplied)
            return Task.CompletedTask;

        _profileFocusApplied = true;
        ApplyInitialTvFocusAsync().FireAndForget();
        return Task.CompletedTask;
    }

    private void ShowCarousel()
    {
        _loading = false;
        _profileFocusApplied = false;
    }

    private async Task SelectUserAsync(LocalUser user)
    {
        if (_editing)
            return;

        if (_selecting)
        {
            PerfTrace.Note("select-user ignored: a selection is already running");
            return;
        }

        _selecting = true;
        PerfTrace.Note("select-user start reconnect=" + NeedsReconnect(user) + " pin=" + user.HasPin);
        try
        {
            SharedProfileSession.ClearActiveGroup();

            // Already marked expired: open device-link / browser login on purpose.
            // A just-discovered dead RT stays on this picker so another profile can be chosen.
            if (NeedsReconnect(user))
            {
                await ReconnectExpiredProfileAsync(user);
                return;
            }

            if (user.HasPin)
            {
                var pinValid = await PromptPinAsync(user);
                if (!pinValid)
                    return;
            }

            await AuthenticateUserAsync(user, allowInteractiveReconnect: true);
        }
        finally
        {
            _selecting = false;
        }
    }

    private async Task SelectGroupAsync(SharedProfileDto group)
    {
        if (_editing || _selecting)
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
        var member = FindLocalMember(group);
        if (member is null)
        {
            Snackbar.Add(L["HostNotOnDevice"], K7Severity.Error);
            return;
        }

        if (NeedsReconnect(member))
        {
            await ReconnectExpiredProfileAsync(member, group);
            return;
        }

        if (group.HasPin)
        {
            var pinValid = await PromptGroupPinAsync(group);
            if (!pinValid)
                return;
        }

        // The group PIN is the only lock on this card. The host account PIN applies when
        // selecting that user, not when entering the shared profile.

        _loading = true;
        _showOfflineButton = false;
        _pendingUser = member;
        StateHasChanged();
        StartOfflineTimer();

        try
        {
            var success = await AuthService.SwitchToUserAsync(member.IdentityUserId);
            CancelOfflineTimer();
            if (success)
            {
                SharedProfileSession.SetActiveGroup(group);
                LocalUserService.SetLastActiveId(member.IdentityUserId);
                RecordSharedProfileSelection(group.Id);
                Navigation.NavigateTo("/");
            }
            else
            {
                Snackbar.Add(string.Format(L["SessionExpired"], member.UserName), K7Severity.Error);
                // TV: stay on the picker with a reconnect badge. Click again (or +) to link.
                if (_isTv)
                {
                    ReloadLocalUsers();
                    ShowCarousel();
                }
                else
                {
                    await ReconnectExpiredProfileAsync(member, group);
                }
            }
        }
        catch (HttpRequestException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(member);
            SharedProfileSession.SetActiveGroup(group);
            LocalUserService.SetLastActiveId(member.IdentityUserId);
            RecordSharedProfileSelection(group.Id);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (TaskCanceledException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(member);
            SharedProfileSession.SetActiveGroup(group);
            LocalUserService.SetLastActiveId(member.IdentityUserId);
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

    private async Task AuthenticateUserAsync(LocalUser user, bool allowInteractiveReconnect)
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
                Snackbar.Add(string.Format(L["SessionExpired"], user.UserName), K7Severity.Error);
                // Solo auto-login must not start LoginAsync under the spinner (browser wait /
                // failed callback left an infinite load). On TV keep the picker so another
                // profile can be chosen; click the expired card or + to open device-link.
                if (allowInteractiveReconnect && !_isTv)
                    await ReconnectExpiredProfileAsync(user);
                else
                {
                    ReloadLocalUsers();
                    ShowCarousel();
                }
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
                ReloadLocalUsers();
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

    /// <summary>
    /// Interactive re-login after a dead refresh token. Mirrors AddUserAsync post-success so we
    /// leave select-profile, refresh local cards (reconnect badge), and restore a shared group.
    /// </summary>
    private async Task ReconnectExpiredProfileAsync(LocalUser user, SharedProfileDto? group = null)
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
            ReloadLocalUsers();

            if (authState.User.Identity?.IsAuthenticated != true)
            {
                ShowCarousel();
                return;
            }

            var subject = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? authState.User.FindFirst("sub")?.Value;

            if (!string.IsNullOrEmpty(subject))
            {
                LocalUserService.SetLastActiveId(subject);
                var active = _users.FirstOrDefault(u =>
                    string.Equals(u.IdentityUserId, subject, StringComparison.Ordinal));
                if (active is not null)
                    MarkSoloUnlockedIfNeeded(active);
            }

            if (group is not null
                && !string.IsNullOrEmpty(subject)
                && IsGroupMember(group, subject))
            {
                SharedProfileSession.SetActiveGroup(group);
                RecordSharedProfileSelection(group.Id);
            }
            else if (!string.IsNullOrEmpty(subject))
            {
                RecordUserSelection(subject);
            }

            Navigation.NavigateTo("/");
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            ReloadLocalUsers();
            ShowCarousel();
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

    private static bool NeedsReconnect(LocalUser user) =>
        string.IsNullOrEmpty(user.RefreshToken);

    private static string GetProfileCardClass(bool needsReconnect, bool isGroup = false, bool editing = false, bool hidden = false)
    {
        var classes = isGroup
            ? "select-profile-card select-profile-card--group my-2"
            : "select-profile-card my-2";

        if (!editing)
            classes += " focusable";

        if (needsReconnect)
            classes += " select-profile-card--expired";

        if (hidden)
            classes += " select-profile-card--hidden";

        if (editing)
            classes += " select-profile-card--editing";

        return classes;
    }

    private async Task BeginEditAsync()
    {
        if (_editing || (_users.Count == 0 && _pinnedGroups.Count == 0))
            return;

        _hiddenBaseline = new HashSet<string>(_hiddenUserIds, StringComparer.Ordinal);
        _entries = BuildEntries(includeHidden: true);
        if (_entries.Count == 0)
            return;

        _editBaseline = [.. _entries];
        _editing = true;
        await BindEditBackAsync(true);
        await RefreshEditChromeAsync();
    }

    private async Task CancelEditAsync()
    {
        if (!_editing)
            return;

        if (_hiddenBaseline is not null)
            _hiddenUserIds = new HashSet<string>(_hiddenBaseline, StringComparer.Ordinal);

        _hiddenBaseline = null;
        _editBaseline = null;
        _editing = false;
        RebuildEntries();
        await BindEditBackAsync(false);
        await RefreshEditChromeAsync();
    }

    private async Task ApplyEditAsync()
    {
        if (!_editing)
            return;

        var baseline = _editBaseline ?? [];
        _editBaseline = null;
        _editing = false;

        var kept = _entries.Select(entry => entry.Token).ToHashSet(StringComparer.Ordinal);
        SaveOrder();

        foreach (var entry in baseline)
        {
            if (kept.Contains(entry.Token))
                continue;

            if (entry.User is { } user)
            {
                _hiddenUserIds.Remove(user.IdentityUserId);
                LocalUserService.Remove(user.IdentityUserId);
            }
            else if (entry.Group is { } group)
            {
                SharedProfileDevicePin.SetPinned(group.Id, false);
                SharedProfileCache.Remove(group.Id);
                if (SharedProfileSession.ActiveGroupId == group.Id)
                    SharedProfileSession.ClearActiveGroup();
            }
        }

        _hiddenBaseline = null;
        ProfilePickerHiddenUsers.Write(Storage, _hiddenUserIds);

        await BindEditBackAsync(false);
        _users = LocalUserService.GetAll();
        ReloadPinnedGroups();
        await RefreshEditChromeAsync();
    }

    private async Task RefreshEditChromeAsync()
    {
        StateHasChanged();
        if (!_isTv)
            return;

        await Task.Yield();
        await SpatialNav.RefreshAsync();
    }

    private async Task BindEditBackAsync(bool enabled)
    {
        _editBackBinder ??= new SelectionModeKeyboardBinder(SpatialNav, CancelEditFromBack, static () => { });
        await _editBackBinder.SetEnabledAsync(enabled);
    }

    private void CancelEditFromBack() => _ = InvokeAsync(CancelEditAsync);

    public async ValueTask DisposeAsync()
    {
        if (_editBackBinder is not null)
            await _editBackBinder.DisposeAsync();
    }

    private void MoveEntry(int index, int delta)
    {
        var target = index + delta;
        if ((uint)index >= (uint)_entries.Count || target < 0 || target >= _entries.Count)
            return;

        (_entries[index], _entries[target]) = (_entries[target], _entries[index]);
    }

    private async Task<bool> AllowUserRemovalAsync(LocalUser user)
    {
        if (!user.HasPin)
            return true;

        var canVerify = (Connectivity.IsOnline && user.UserId is not null) || user.PinHash is not null;
        if (!canVerify)
        {
            Snackbar.Add(L["PinRequiredOnline"], K7Severity.Error);
            return false;
        }

        return await PromptPinAsync(user);
    }

    /// <summary>
    /// A live shared profile keeps its PIN. A profile the server no longer has can be
    /// removed without that PIN, so a forgotten code does not trap the leftover card.
    /// </summary>
    private async Task<bool> AllowSharedProfileRemovalAsync(SharedProfileDto group)
    {
        if (!group.HasPin)
            return true;

        bool? exists = null;
        if (Connectivity.IsOnline)
            exists = await SharedProfileService.ProfileExistsAsync(group.Id);

        if (exists == false)
            return true;

        var canVerify = exists == true || group.PinHash is not null;
        if (!canVerify)
        {
            Snackbar.Add(L["PinRequiredOnline"], K7Severity.Error);
            return false;
        }

        return await PromptGroupPinAsync(group);
    }

    private async Task RemoveEntryAsync(PickerEntry entry)
    {
        if (entry.User is { } user)
        {
            if (!await AllowUserRemovalAsync(user))
                return;

            var name = user.DisplayName ?? user.UserName;
            var confirmed = await DialogService.ShowMessageBoxAsync(
                L["RemoveUserTitle"],
                string.Format(L["RemoveUserConfirm"], name),
                yesText: L["RemoveProfile"],
                cancelText: S["Cancel"]);
            if (confirmed != true)
                return;

            _entries.RemoveAll(item => item.Token == entry.Token);
        }
        else if (entry.Group is { } group)
        {
            if (!await AllowSharedProfileRemovalAsync(group))
                return;

            var confirmed = await DialogService.ShowMessageBoxAsync(
                L["RemoveGroupTitle"],
                string.Format(L["RemoveGroupConfirm"], group.Name),
                yesText: L["RemoveProfile"],
                cancelText: S["Cancel"]);
            if (confirmed != true)
                return;

            _entries.RemoveAll(item => item.Token == entry.Token);
        }
    }

    private LocalUser? FindLocalMember(SharedProfileDto group)
    {
        var candidates = _users.Where(user => IsGroupMember(group, user.IdentityUserId)).ToList();
        return candidates.FirstOrDefault(user => !NeedsReconnect(user)) ?? candidates.FirstOrDefault();
    }

    private static bool IsGroupMember(SharedProfileDto group, string? identityUserId)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return false;

        if (string.Equals(group.HostIdentityUserId, identityUserId, StringComparison.Ordinal))
            return true;

        return group.Members.Any(member =>
            string.Equals(member.IdentityUserId, identityUserId, StringComparison.Ordinal));
    }

    private bool GroupNeedsReconnect(SharedProfileDto group)
    {
        var member = FindLocalMember(group);
        return member is not null && NeedsReconnect(member);
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
