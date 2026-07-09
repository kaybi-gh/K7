using System.Security.Claims;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.Services.K7Server;

public class CustomAuthenticationStateProvider : ICustomAuthenticationStateProvider
{
    private readonly NavigationManager _navigationManager;

    public CustomAuthenticationStateProvider(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public Task LoginAsync(CancellationToken cancellationToken = default)
    {
        var redirectUri = Uri.EscapeDataString(_navigationManager.Uri);
        _navigationManager.NavigateTo($"{_navigationManager.BaseUri}api/authentication/login?returnUrl={redirectUri}", forceLoad: true);
        //_navigationManager.NavigateTo($"{_navigationManager.BaseUri}connect/authorize", forceLoad: true);
        return Task.CompletedTask;
    }

    public Task LoginAsGuestAsync(CancellationToken cancellationToken = default)
    {
        var redirectUri = Uri.EscapeDataString(_navigationManager.Uri);
        _navigationManager.NavigateTo($"{_navigationManager.BaseUri}api/authentication/login?returnUrl={redirectUri}", forceLoad: true);
        return Task.CompletedTask;
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var redirectUri = Uri.EscapeDataString(_navigationManager.ToBaseRelativePath(_navigationManager.Uri));
        _navigationManager.NavigateTo($"{_navigationManager.BaseUri}account/logout?returnUrl={redirectUri}", forceLoad: true);
        return Task.CompletedTask;
    }

    public Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Device code flow is not supported in the web client.");
    }

    public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> SwitchToUserAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("User switching is not supported in the web client.");
    }

    public void SignInOffline(LocalUser user)
    {
        // Not applicable for web client
    }

    public Task RefreshStoredUserProfilesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
