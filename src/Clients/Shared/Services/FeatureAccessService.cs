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
        var user = authState.User;
        if (user.IsInRole(Roles.Administrator)) return Roles.Administrator;
        if (user.IsInRole(Roles.User)) return Roles.User;
        if (user.IsInRole(Roles.Guest)) return Roles.Guest;
        return null;
    }
}
