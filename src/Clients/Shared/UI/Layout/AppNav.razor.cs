using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class AppNav : IDisposable
{
    private bool _exploreOpen;
    private bool _profileMenuOpen;
    private ElementReference _profilePopoverRef;
    private ElementReference _explorePopoverRef;
    private ElementReference _profileButtonRef;
    private DotNetObjectReference<LayerCloseCallback>? _exploreCloseRef;
    private DotNetObjectReference<LayerCloseCallback>? _profileCloseRef;
    private List<LibraryDto> _libraries = [];
    private string _badgeClass = "offline";
    private string _badgeTitle = string.Empty;

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private ICustomAuthenticationStateProvider CustomAuthenticationStateProvider { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IStringLocalizer<AppNav> L { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    public bool IsAnyMenuOpen => _exploreOpen || _profileMenuOpen;

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
        HubClient.ConnectionStateChanged += OnConnectionStateChanged;
        UpdateBadge(HubClient.State);

        try
        {
            _libraries = await K7ServerService.GetLibrariesAsync();
        }
        catch
        {
            _libraries = [];
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
        InvokeAsync(StateHasChanged);
    }

    public async Task ToggleExplore()
    {
        var wasOpen = _exploreOpen;
        _exploreOpen = !_exploreOpen;

        if (_profileMenuOpen)
        {
            _profileMenuOpen = false;
            try { await SpatialNav.PopLayerAsync(_profilePopoverRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }

        if (_exploreOpen)
        {
            StateHasChanged();
            await Task.Yield();
            _exploreCloseRef?.Dispose();
            _exploreCloseRef = DotNetObjectReference.Create(new LayerCloseCallback(() =>
            {
                _exploreOpen = false;
                InvokeAsync(StateHasChanged);
            }));
            try
            {
                await SpatialNav.PushLayerAsync(_explorePopoverRef, "popover", new SpatialNavLayerOptions
                {
                    OnClose = _exploreCloseRef
                });
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
        else if (wasOpen)
        {
            try { await SpatialNav.PopLayerAsync(_explorePopoverRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
    }

    public async Task ToggleProfile()
    {
        var wasOpen = _profileMenuOpen;
        _profileMenuOpen = !_profileMenuOpen;

        if (_exploreOpen)
        {
            _exploreOpen = false;
            try { await SpatialNav.PopLayerAsync(_explorePopoverRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }

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
        if (_exploreOpen)
        {
            _exploreOpen = false;
            try { await SpatialNav.PopLayerAsync(_explorePopoverRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
        if (_profileMenuOpen)
        {
            _profileMenuOpen = false;
            try { await SpatialNav.PopLayerAsync(_profilePopoverRef); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
    }

    public void CloseAll()
    {
        _ = CloseAllAsync();
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

    private static string GetLibraryIcon(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => Phosphor.FilmStrip,
        LibraryMediaType.Serie => Phosphor.Television,
        LibraryMediaType.Music => Phosphor.MusicNotes,
        _ => Phosphor.Folder
    };

    private static string GetLibraryIconName(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => "film-strip",
        LibraryMediaType.Serie => "television",
        LibraryMediaType.Music => "music-notes",
        _ => "folder"
    };

    private static (string GradientStart, string GradientEnd, string IconColor) GetLibraryCardColors(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => ("rgba(120,30,30,0.85)", "rgba(20,10,10,0.9)", "rgba(180,40,40,0.6)"),
        LibraryMediaType.Serie => ("rgba(20,60,120,0.85)", "rgba(10,15,40,0.9)", "rgba(30,80,160,0.6)"),
        LibraryMediaType.Music => ("rgba(80,20,100,0.85)", "rgba(15,10,30,0.9)", "rgba(110,30,140,0.6)"),
        _ => ("rgba(30,60,40,0.85)", "rgba(10,20,15,0.9)", "rgba(40,80,55,0.6)")
    };

    private void NavigateAndClose(string url)
    {
        CloseAll();
        NavigationManager.NavigateTo(url);
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
            _libraries = await K7ServerService.GetLibrariesAsync();
        }
        catch
        {
            _libraries = [];
        }
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _exploreCloseRef?.Dispose();
        _profileCloseRef?.Dispose();
    }
}
