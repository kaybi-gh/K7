using System.Runtime.CompilerServices;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Role-scoped menu capability flags shared across remounted list/grid cells
/// (Virtualize recycle) so each row does not re-query auth.
/// </summary>
internal static class MediaCardMenuCapabilities
{
    private static readonly ConditionalWeakTable<IFeatureAccessService, Cache> Caches = new();

    public sealed class Cache
    {
        public string? Role;
        public bool CanRate;
        public bool CanCreateLibrary;
        public bool CanSetWatchState;
        public bool CanEditMetadata;
    }

    public static async Task<Cache> GetAsync(IFeatureAccessService featureAccess)
    {
        var cache = Caches.GetOrCreateValue(featureAccess);
        var role = await featureAccess.GetRoleAsync();
        if (cache.Role == role)
            return cache;

        cache.CanRate = await featureAccess.HasCapabilityAsync(Capability.CanRate);
        cache.CanCreateLibrary = await featureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
        cache.CanSetWatchState = role is Roles.User or Roles.Administrator;
        cache.CanEditMetadata = role == Roles.Administrator;
        cache.Role = role;
        return cache;
    }
}
