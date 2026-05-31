using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibraries;

//[Authorize]
public record GetLibrariesQuery : IRequest<IEnumerable<Library>>;

public class GetLibrariesQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetLibrariesQuery, IEnumerable<Library>>
{
    public async Task<IEnumerable<Library>> Handle(GetLibrariesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Libraries
            .AsNoTracking()
            .Include(l => l.PeerServer)
            .AsQueryable();

        if (currentUser.Id is { } userId)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            query = query.Where(l => !excludedLibraryIds.Contains(l.Id));
        }

        return await query.OrderBy(t => t.Title).ToListAsync(cancellationToken);
    }
}
