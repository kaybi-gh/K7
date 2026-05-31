using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibrary;

[Authorize]
public record GetLibraryQuery(Guid Id) : IRequest<Library>;

public class GetLibraryQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetLibraryQuery, Library>
{
    public async Task<Library> Handle(GetLibraryQuery request, CancellationToken cancellationToken)
    {
        var query = context.Libraries
            .AsNoTracking()
            .Include(l => l.PeerServer)
            .Where(x => x.Id == request.Id);

        if (currentUser.Id is { } userId)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            query = query.Where(x => !excludedLibraryIds.Contains(x.Id));
        }

        var entity = await query.SingleOrDefaultAsync(cancellationToken);
        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
