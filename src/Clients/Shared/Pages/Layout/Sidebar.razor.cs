using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.JSInterop;
using MudBlazor;

namespace K7.Clients.Shared.Pages.Layout;

public partial class Sidebar
{
    string _debouncedText = "";
    private DotNetObjectReference<Sidebar>? _dotNetRef;
    private List<LibraryDto> _libraries = [];

    protected override void OnInitialized()
    {
        SidebarService.IsOpenOnChange += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await K7ServerService.GetLibrariesAsync();
        }
        catch
        {
            _libraries = [];
        }
    }

    private static string GetLibraryIcon(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => Icons.Material.Filled.Theaters,
        LibraryMediaType.Serie => Icons.Material.Filled.Tv,
        LibraryMediaType.Music => Icons.Material.Filled.MusicNote,
        _ => Icons.Material.Filled.Folder
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("SpatialNavigation.setSidebarCallback", _dotNetRef);
        }
    }

    [JSInvokable]
    public void SetSidebarOpen(bool open)
    {
        if (SidebarService.IsOpen != open)
        {
            SidebarService.IsOpen = open;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        SidebarService.IsOpenOnChange -= StateHasChanged;
        _dotNetRef?.Dispose();
    }

    private void Search()
    {
        
    }

    private async Task Login()
    {
        await CustomAuthenticationStateProvider.LoginAsync();
    }

    private async void Logout()
    {
        await CustomAuthenticationStateProvider.LogoutAsync();
    }
}
