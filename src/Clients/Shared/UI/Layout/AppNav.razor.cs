using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
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

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private ICustomAuthenticationStateProvider CustomAuthenticationStateProvider { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IStringLocalizer<AppNav> L { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    public bool IsAnyMenuOpen => _profileMenuOpen;

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
        HubClient.ConnectionStateChanged += OnConnectionStateChanged;
        UpdateBadge(HubClient.State);
        UpdateActiveNav();
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
        }
        catch { }
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _profileCloseRef?.Dispose();
    }
}
