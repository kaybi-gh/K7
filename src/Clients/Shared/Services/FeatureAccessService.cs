using System.Security.Claims;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.Services;

public class FeatureAccessService(AuthenticationStateProvider authStateProvider) : IFeatureAccessService
{
    public async Task<bool> HasCapabilityAsync(Capability capability)
    {
        var role = await GetRoleAsync();
        if (role is null) return false;
        return DefaultCapabilities.ForRole(role).Contains(capability);
    }

    public async Task<string?> GetRoleAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var roles = authState.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
        if (roles.Contains(Roles.Administrator)) return Roles.Administrator;
        if (roles.Contains(Roles.User)) return Roles.User;
        if (roles.Contains(Roles.Guest)) return Roles.Guest;
        return null;
    }
}
