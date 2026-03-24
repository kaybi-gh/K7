using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace K7.Clients.Shared.Pages.Layout;

public partial class MobileFooter : IDisposable
{
    private bool _libraryDrawerOpen;
    private bool _musicDrawerOpen;
    private bool _menuDrawerOpen;
    private List<LibraryDto> _libraries = [];

    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private BackButtonService BackButton { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Nav.LocationChanged += OnLocationChanged;
        BackButton.Register(HandleBackButton);

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

    public void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
        BackButton.Unregister();
    }

    private static string GetLibraryIcon(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => Icons.Material.Filled.Theaters,
        LibraryMediaType.Serie => Icons.Material.Filled.Tv,
        LibraryMediaType.Music => Icons.Material.Filled.MusicNote,
        _ => Icons.Material.Filled.Folder
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
        NavigationManager.NavigateTo("/select-user");
    }
}