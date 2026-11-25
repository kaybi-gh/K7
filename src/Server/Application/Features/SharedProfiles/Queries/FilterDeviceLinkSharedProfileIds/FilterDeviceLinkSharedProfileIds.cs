using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.SharedProfiles.Queries.FilterDeviceLinkSharedProfileIds;

/// <summary>
/// Keeps the profiles the device account actually belongs to. The link form can post any id.
/// </summary>
public record FilterDeviceLinkSharedProfileIdsQuery(string IdentityUserId, IReadOnlyList<Guid> RequestedIds)
    : IRequest<IReadOnlyList<Guid>>;

public class FilterDeviceLinkSharedProfileIdsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<FilterDeviceLinkSharedProfileIdsQuery, IReadOnlyList<Guid>>
{
    public const int MaxIds = 20;

    public async Task<IReadOnlyList<Guid>> Handle(
        FilterDeviceLinkSharedProfileIdsQuery request,
        CancellationToken cancellationToken)
    {
        var requested = request.RequestedIds.Where(id => id != Guid.Empty).Distinct().Take(MaxIds).ToList();
        if (requested.Count == 0 || string.IsNullOrEmpty(request.IdentityUserId))
            return [];

        var domainUserId = await context.Users
            .AsNoTracking()
            .Where(user => user.IdentityUserId == request.IdentityUserId)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (domainUserId is null)
            return [];

        var allowed = await context.SharedProfiles
            .AsNoTracking()
            .Where(group => requested.Contains(group.Id)
                            && (group.HostUserId == domainUserId
                                || group.Members.Any(member => member.UserId == domainUserId)))
            .Select(group => group.Id)
            .ToListAsync(cancellationToken);

        var allowedSet = allowed.ToHashSet();
        return requested.Where(allowedSet.Contains).ToList();
    }
}
