using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Services;

public sealed record ViewingGroupPlaybackContext(
    Guid ViewingGroupId,
    string GroupName,
    IReadOnlyList<Guid> CoViewerUserIds);

public interface IViewingGroupPlaybackResolver
{
    Task<ViewingGroupPlaybackContext?> ResolveAsync(
        Guid viewingGroupId,
        Guid hostUserId,
        CancellationToken cancellationToken = default);
}

public class ViewingGroupPlaybackResolver(IApplicationDbContext context) : IViewingGroupPlaybackResolver
{
    public async Task<ViewingGroupPlaybackContext?> ResolveAsync(
        Guid viewingGroupId,
        Guid hostUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await context.ViewingGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == viewingGroupId, cancellationToken);

        if (group is null || group.HostUserId != hostUserId)
            return null;

        if (!group.Members.Any(m => m.UserId == hostUserId))
            return null;

        var coViewers = group.Members
            .Where(m => m.UserId != hostUserId)
            .Select(m => m.UserId)
            .ToList();

        return new ViewingGroupPlaybackContext(group.Id, group.Name, coViewers);
    }
}
