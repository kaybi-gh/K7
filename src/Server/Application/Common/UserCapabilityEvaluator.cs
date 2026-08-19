using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common;

public static class UserCapabilityEvaluator
{
    public static async Task<string> GetRoleAsync(
        IApplicationDbContext context,
        IIdentityService identityService,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var identityUserId = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.IdentityUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(identityUserId))
            return Roles.Guest;

        var roles = await identityService.GetRolesAsync(identityUserId);
        return roles?.FirstOrDefault() ?? Roles.Guest;
    }

    public static async Task<bool> HasAsync(
        IApplicationDbContext context,
        IIdentityService identityService,
        Guid userId,
        Capability capability,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleAsync(context, identityService, userId, cancellationToken);
        return await HasForRoleAsync(context, userId, role, capability, cancellationToken);
    }

    public static async Task<bool> HasForRoleAsync(
        IApplicationDbContext context,
        Guid userId,
        string role,
        Capability capability,
        CancellationToken cancellationToken = default)
    {
        var overrideEnabled = await context.UserCapabilityOverrides
            .AsNoTracking()
            .Where(o => o.UserId == userId && o.Capability == capability)
            .Select(o => (bool?)o.Enabled)
            .FirstOrDefaultAsync(cancellationToken);

        if (overrideEnabled is not null)
            return overrideEnabled.Value;

        return DefaultCapabilities.ForRole(role).Contains(capability);
    }
}
