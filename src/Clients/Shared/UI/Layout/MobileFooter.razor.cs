using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.SignalR.Client;

namespace K7.Clients.Shared.UI.Layout;

public partial class MobileFooter : IDisposable
{
    private bool _libraryDrawerOpen;
    private bool _musicDrawerOpen;
    private bool _menuDrawerOpen;
    private List<LibraryDto> _libraries = [];
    private string _badgeClass = "offline";
    private string _badgeTitle = string.Empty;

    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private BackButtonService BackButton { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Nav.LocationChanged += OnLocationChanged;
        BackButton.Register(HandleBackButton);
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
        if (!IsAnyDrawerOpen)
            return false;

        CloseDrawers();
        InvokeAsync(StateHasChanged);
        return true;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        CloseDrawers();
        InvokeAsync(StateHasChanged);
    }

    private void ToggleLibraryDrawer()
    {
        _libraryDrawerOpen = !_libraryDrawerOpen;
        _musicDrawerOpen = false;
        _menuDrawerOpen = false;
    }

    private void ToggleMusicDrawer()
    {
        _musicDrawerOpen = !_musicDrawerOpen;
        _libraryDrawerOpen = false;
        _menuDrawerOpen = false;
    }

    private void ToggleMenuDrawer()
    {
        _menuDrawerOpen = !_menuDrawerOpen;
        _libraryDrawerOpen = false;
        _musicDrawerOpen = false;
    }

    public bool IsAnyDrawerOpen => _libraryDrawerOpen || _musicDrawerOpen || _menuDrawerOpen;

    public void CloseDrawers()
    {
        _libraryDrawerOpen = false;
        _musicDrawerOpen = false;
        _menuDrawerOpen = false;
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

    public void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        BackButton.Unregister();
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

    private static string GetLibraryIcon(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => Phosphor.FilmStrip,
        LibraryMediaType.Serie => Phosphor.Television,
        LibraryMediaType.Music => Phosphor.MusicNotes,
        _ => Phosphor.Folder
    };

    private void SwitchUser()
    {
        CloseDrawers();
        NavigationManager.NavigateTo("/select-user");
    }

    private async Task Login()
    {
        CloseDrawers();
        await CustomAuthenticationStateProvider.LoginAsync();
    }

    private async Task Logout()
    {
        CloseDrawers();
        await CustomAuthenticationStateProvider.LogoutAsync();
        NavigationManager.NavigateTo("/");
    }
}
