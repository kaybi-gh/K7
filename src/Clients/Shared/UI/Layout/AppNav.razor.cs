using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class AppNav : IDisposable
{
    private bool _exploreOpen;
    private bool _profileMenuOpen;
    private ElementReference _profilePopoverRef;
    private ElementReference _profileButtonRef;
    private bool _preventPopoverKeyDefault;
    private List<LibraryDto> _libraries = [];
    private string _badgeClass = "offline";
    private string _badgeTitle = string.Empty;

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ICustomAuthenticationStateProvider CustomAuthenticationStateProvider { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IStringLocalizer<AppNav> L { get; set; } = default!;
    [Inject] private BackButtonService BackButton { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    public bool IsAnyMenuOpen => _exploreOpen || _profileMenuOpen;

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
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
        if (!IsAnyMenuOpen)
            return false;

        CloseAll();
        InvokeAsync(StateHasChanged);
        return true;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        CloseAll();
        InvokeAsync(StateHasChanged);
    }

    public void ToggleExplore()
    {
        _exploreOpen = !_exploreOpen;
        _profileMenuOpen = false;
    }

    public async Task ToggleProfile()
    {
        _profileMenuOpen = !_profileMenuOpen;
        _exploreOpen = false;
        if (_profileMenuOpen)
        {
            StateHasChanged();
            await Task.Yield();
            try { await JS.InvokeVoidAsync("K7.focusFirstIn", _profilePopoverRef); }
            catch (JSException) { }
        }
        else
        {
            StateHasChanged();
            await Task.Yield();
            try { await _profileButtonRef.FocusAsync(); }
            catch { }
        }
    }

    public async Task OnProfileMenuKeyDown(KeyboardEventArgs e)
    {
        _preventPopoverKeyDefault = false;
        switch (e.Code)
        {
            case "Escape" or "BrowserBack" or "GoBack":
                _preventPopoverKeyDefault = true;
                CloseAll();
                StateHasChanged();
                await Task.Yield();
                try { await _profileButtonRef.FocusAsync(); }
                catch { }
                break;
            case "ArrowDown":
                _preventPopoverKeyDefault = true;
                try { await JS.InvokeVoidAsync("K7.focusDirection", _profilePopoverRef, "next"); }
                catch (JSException) { }
                break;
            case "ArrowUp":
                _preventPopoverKeyDefault = true;
                try { await JS.InvokeVoidAsync("K7.focusDirection", _profilePopoverRef, "prev"); }
                catch (JSException) { }
                break;
            case "Tab":
                _preventPopoverKeyDefault = true;
                try { await JS.InvokeVoidAsync("K7.focusDirection", _profilePopoverRef, e.ShiftKey ? "prev" : "next"); }
                catch (JSException) { }
                break;
        }
    }

    public void CloseAll()
    {
        _exploreOpen = false;
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
        BackButton.Unregister();
    }
}
