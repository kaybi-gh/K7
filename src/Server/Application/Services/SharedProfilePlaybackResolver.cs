using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Services;

public sealed record SharedProfilePlaybackContext(
    Guid SharedProfileId,
    string GroupName,
    IReadOnlyList<Guid> CoViewerUserIds);

public interface ISharedProfilePlaybackResolver
{
    Task<SharedProfilePlaybackContext?> ResolveAsync(
        Guid sharedProfileId,
        Guid hostUserId,
        CancellationToken cancellationToken = default);
}

public class SharedProfilePlaybackResolver(IApplicationDbContext context) : ISharedProfilePlaybackResolver
{
    public async Task<SharedProfilePlaybackContext?> ResolveAsync(
        Guid sharedProfileId,
        Guid hostUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await context.SharedProfiles
            .AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == sharedProfileId, cancellationToken);

        if (group is null || group.HostUserId != hostUserId)
            return null;

        if (!group.Members.Any(m => m.UserId == hostUserId))
            return null;

        var coViewers = group.Members
            .Where(m => m.UserId != hostUserId)
            .Select(m => m.UserId)
            .ToList();

        return new SharedProfilePlaybackContext(group.Id, group.Name, coViewers);
    }
}
