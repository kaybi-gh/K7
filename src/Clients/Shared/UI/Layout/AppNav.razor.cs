using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class AppNav : IDisposable
{
    private bool _profileMenuOpen;
    private ElementReference _profilePopoverRef;
    private ElementReference _profileButtonRef;
    private DotNetObjectReference<LayerCloseCallback>? _profileCloseRef;
    private string _activeNav = "/";
    private string _badgeClass = "offline";
    private string _badgeTitle = string.Empty;
    private string? _avatarUrl;
    private string _avatarInitial = "?";
    private string? _viewingGroupLabel;
    private bool _chatOpen;
    private readonly Dictionary<Guid, string> _knownParticipants = [];

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private ICustomAuthenticationStateProvider CustomAuthenticationStateProvider { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IStringLocalizer<AppNav> L { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;
    [Inject] private IUserAdminService UserService { get; set; } = default!;
    [Inject] private ISyncPlayService SyncPlay { get; set; } = default!;
    [Inject] private IViewingGroupSessionService? ViewingGroupSession { get; set; }
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    public bool IsAnyMenuOpen => _profileMenuOpen;

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
        HubClient.ConnectionStateChanged += OnConnectionStateChanged;
        SyncPlay.GroupUpdated += OnSyncPlayGroupUpdated;
        SyncPlay.ChatMessageReceived += OnChatMessageReceived;
        SyncPlay.ErrorReceived += OnSyncPlayErrorReceived;
        ViewingGroupSession?.ActiveGroupChanged += OnViewingGroupChanged;
        UpdateBadge(HubClient.State);
        UpdateViewingGroupLabel();
        UpdateActiveNav();
        await LoadAvatarAsync();
    }

    private async Task LoadAvatarAsync()
    {
        try
        {
            var me = await UserService.GetCurrentUserAsync();
            if (me is not null)
            {
                _avatarUrl = me.AvatarUrl;
                var name = me.DisplayName ?? me.UserName;
                _avatarInitial = string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
            }
        }
        catch
        {
            // Avatar not critical
        }
    }

    private bool HandleBackButton()
    {
        if (!IsAnyMenuOpen)
            return false;

        _ = CloseAllAsync();
        InvokeAsync(StateHasChanged);
        return true;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _ = CloseAllAsync();
        UpdateActiveNav();
        InvokeAsync(StateHasChanged);
    }

    private void UpdateActiveNav()
    {
        var path = new Uri(NavigationManager.Uri).AbsolutePath;
        if (path == "/")
            _activeNav = "/";
        else if (path.StartsWith("/explore", StringComparison.OrdinalIgnoreCase))
            _activeNav = "/explore";
        else if (path.StartsWith("/my-space", StringComparison.OrdinalIgnoreCase))
            _activeNav = "/my-space";
        else if (path.StartsWith("/search", StringComparison.OrdinalIgnoreCase))
            _activeNav = "/search";
        else
            _activeNav = "";
    }

    private void Navigate(string url)
    {
        NavigationManager.NavigateTo(url);
    }

    public async Task ToggleProfile()
    {
        var wasOpen = _profileMenuOpen;
        _profileMenuOpen = !_profileMenuOpen;

        if (_profileMenuOpen)
        {
            StateHasChanged();
            await Task.Yield();
            _profileCloseRef?.Dispose();
            _profileCloseRef = DotNetObjectReference.Create(new LayerCloseCallback(() =>
            {
                _profileMenuOpen = false;
                InvokeAsync(StateHasChanged);
            }));
            try
            {
                await SpatialNav.PushLayerAsync(_profilePopoverRef, "popover", new SpatialNavLayerOptions
                {
                    OnClose = _profileCloseRef
                });
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
        else if (wasOpen)
        {
            try { await SpatialNav.PopLayerAsync(_profilePopoverRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
            StateHasChanged();
            await Task.Yield();
            try { await _profileButtonRef.FocusAsync(); }
            catch { }
        }
    }

    public async Task CloseAllAsync()
    {
        if (_profileMenuOpen)
        {
            _profileMenuOpen = false;
            try { await SpatialNav.PopLayerAsync(_profilePopoverRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
    }

    public void CloseAll()
    {
        _profileMenuOpen = false;
    }

    private void OnConnectionStateChanged(HubConnectionState state)
    {
        UpdateBadge(state);
        InvokeAsync(StateHasChanged);
    }

    private void UpdateBadge(HubConnectionState state)
    {
        (_badgeClass, _badgeTitle) = state switch
        {
            HubConnectionState.Connected => ("connected", L["Connected"]),
            _ => ("offline", L["Reconnecting"])
        };
    }

    private void SwitchUser()
    {
        CloseAll();
        NavigationManager.NavigateTo("/select-user");
    }

    private async Task Login()
    {
        CloseAll();
        await CustomAuthenticationStateProvider.LoginAsync();
    }

    private async Task Logout()
    {
        CloseAll();
        await CustomAuthenticationStateProvider.LogoutAsync();
        NavigationManager.NavigateTo("/");
    }

    private async void OnAuthStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            await task;
            await LoadAvatarAsync();
        }
        catch { }
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        SyncPlay.GroupUpdated -= OnSyncPlayGroupUpdated;
        SyncPlay.ChatMessageReceived -= OnChatMessageReceived;
        SyncPlay.ErrorReceived -= OnSyncPlayErrorReceived;
        if (ViewingGroupSession is not null)
            ViewingGroupSession.ActiveGroupChanged -= OnViewingGroupChanged;
        if (_profileMenuOpen)
        {
            try { _ = SpatialNav.PopLayerAsync(_profilePopoverRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
        _profileCloseRef?.Dispose();
    }

    private void OnSyncPlayGroupUpdated()
    {
        var participants = SyncPlay.CurrentGroup?.Participants;
        if (participants is not null)
        {
            var isFirstUpdate = _knownParticipants.Count == 0;
            var myUserId = HubClient.ConnectedUserId;
            var currentIds = participants.Select(p => p.DeviceId).ToHashSet();

            if (!isFirstUpdate)
            {
                foreach (var (deviceId, displayName) in _knownParticipants)
                {
                    if (!currentIds.Contains(deviceId))
                    {
                        Snackbar.Add($"{displayName} left", K7Severity.Info);
                    }
                }

                foreach (var p in participants)
                {
                    if (!_knownParticipants.ContainsKey(p.DeviceId) && p.UserId != myUserId)
                    {
                        Snackbar.Add($"{p.DisplayName} joined", K7Severity.Info);
                    }
                }
            }

            _knownParticipants.Clear();
            foreach (var p in participants)
            {
                _knownParticipants[p.DeviceId] = p.DisplayName;
            }
        }
        else
        {
            _knownParticipants.Clear();
        }

        InvokeAsync(StateHasChanged);
    }

    private void OnSyncPlayErrorReceived(string errorCode)
    {
        if (errorCode == "kicked")
        {
            _knownParticipants.Clear();
            Snackbar.Add(L["SyncPlayKicked"], K7Severity.Warning);
        }
    }

    private void OnChatMessageReceived(SyncPlayChatMessageDto message)
    {
        if (_chatOpen || string.IsNullOrEmpty(message.DisplayName) || SyncPlay.IsOwnMessage(message.MessageId))
            return;

        Snackbar.Add($"{message.DisplayName}: {message.Text}", K7Severity.Info);
    }

    private async Task OpenSyncPlayDialog()
    {
        CloseAll();
        _chatOpen = true;
        await DialogService.ShowAsync<SyncPlayDialog>(L["SyncPlay"], options: new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true });
        _chatOpen = false;
    }

    private void OnViewingGroupChanged() => InvokeAsync(() =>
    {
        UpdateViewingGroupLabel();
        StateHasChanged();
    });

    private void UpdateViewingGroupLabel()
    {
        if (DeviceService.GetClientType() == K7.Server.Domain.Enums.ClientType.Web)
        {
            _viewingGroupLabel = null;
            return;
        }

        _viewingGroupLabel = ViewingGroupSession?.ActiveGroup?.Name;
    }
}
