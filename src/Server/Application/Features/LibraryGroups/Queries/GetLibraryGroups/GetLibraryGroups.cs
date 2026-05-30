using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.LibraryGroups.Queries.GetLibraryGroups;

public record GetLibraryGroupsQuery : IRequest<IEnumerable<LibraryGroup>>;

public class GetLibraryGroupsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetLibraryGroupsQuery, IEnumerable<LibraryGroup>>
{
    public async Task<IEnumerable<LibraryGroup>> Handle(GetLibraryGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = context.LibraryGroups
            .AsNoTracking()
            .Include(g => g.CoverPicture)
            .Include(g => g.Libraries)
            .AsQueryable();

        if (currentUser.Id is { } userId)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            query = query.Where(g => g.Libraries.Any(l => !excludedLibraryIds.Contains(l.Id)));
        }

        return await query.OrderBy(g => g.Title).ToListAsync(cancellationToken);
    }
}
