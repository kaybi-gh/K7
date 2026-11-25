using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;

namespace K7.Server.Application.Features.Devices;

public static class DeviceCallerAccess
{
    public static async Task<bool> CallerIsAdministratorAsync(
        IUser user,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(Roles.Administrator))
            return true;

        if (string.IsNullOrWhiteSpace(user.IdentityId))
            return false;

        return await identityService.IsInRoleAsync(user.IdentityId, Roles.Administrator);
    }

    public static async Task<bool> CallerCanAccessAsync(
        IApplicationDbContext context,
        IUser user,
        IIdentityService identityService,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (await CallerIsAdministratorAsync(user, identityService, cancellationToken))
            return true;

        var userId = await user.GetIdAsync(cancellationToken);
        if (userId is null)
            return false;

        return await context.Devices
            .AsNoTracking()
            .AnyAsync(d => d.Id == deviceId && d.Users.Any(u => u.Id == userId.Value), cancellationToken);
    }

    public static async Task EnsureCallerCanAccessAsync(
        IApplicationDbContext context,
        IUser user,
        IIdentityService identityService,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (!await CallerCanAccessAsync(context, user, identityService, deviceId, cancellationToken))
            throw new NotFoundException(deviceId.ToString(), nameof(Device));
    }
}
