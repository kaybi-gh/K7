using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.Services;

public sealed class FeatureAccessService : IFeatureAccessService, IDisposable
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly object _cacheLock = new();
    private string? _cachedRole;
    private bool _hasCachedRole;

    public FeatureAccessService(AuthenticationStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
        _authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    public async Task<bool> HasCapabilityAsync(Capability capability)
    {
        var role = await GetRoleAsync();
        if (role is null) return false;
        return DefaultCapabilities.ForRole(role).Contains(capability);
    }

    public async Task<string?> GetRoleAsync()
    {
        lock (_cacheLock)
        {
            if (_hasCachedRole)
                return _cachedRole;
        }

        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var role = ResolveRole(authState.User);

        lock (_cacheLock)
        {
            _cachedRole = role;
            _hasCachedRole = true;
        }

        return role;
    }

    public void Dispose() =>
        _authStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

    private void OnAuthenticationStateChanged(Task<AuthenticationState> _)
    {
        lock (_cacheLock)
        {
            _hasCachedRole = false;
            _cachedRole = null;
        }
    }

    private static string? ResolveRole(System.Security.Claims.ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.Administrator)) return Roles.Administrator;
        if (user.IsInRole(Roles.User)) return Roles.User;
        if (user.IsInRole(Roles.Guest)) return Roles.Guest;
        return null;
    }
}
