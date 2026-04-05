using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages.Layout;

public partial class Sidebar
{
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    private string _debouncedText = "";
    private DotNetObjectReference<Sidebar>? _dotNetRef;
    private List<LibraryDto> _libraries = [];
    private Color _badgeColor = Color.Default;
    private string _badgeTitle = string.Empty;

    protected override void OnInitialized()
    {
        SidebarService.IsOpenOnChange += StateHasChanged;
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
        HubClient.ConnectionStateChanged += OnConnectionStateChanged;
        UpdateBadge(HubClient.State);
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

    private void OnConnectionStateChanged(HubConnectionState state)
    {
        UpdateBadge(state);
        InvokeAsync(StateHasChanged);
    }

    private void UpdateBadge(HubConnectionState state)
    {
        (_badgeColor, _badgeTitle) = state switch
        {
            HubConnectionState.Connected => (Color.Success, L["Connected"]),
            _ => (Color.Default, L["Reconnecting"])
        };
    }

    public void Dispose()
    {
        SidebarService.IsOpenOnChange -= StateHasChanged;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _dotNetRef?.Dispose();
    }

    private void Search()
    {
        
    }

    private async Task Login()
    {
        await CustomAuthenticationStateProvider.LoginAsync();
    }

    private async Task Logout()
    {
        await CustomAuthenticationStateProvider.LogoutAsync();
        NavigationManager.NavigateTo("/");
    }

    private void SwitchUser()
    {
        NavigationManager.NavigateTo("/select-user");
    }
}
