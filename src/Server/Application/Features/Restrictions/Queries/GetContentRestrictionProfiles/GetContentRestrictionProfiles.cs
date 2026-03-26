using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Restrictions;

namespace K7.Server.Application.Features.Restrictions.Queries.GetContentRestrictionProfiles;

public record GetContentRestrictionProfilesQuery : IRequest<List<ContentRestrictionProfile>>;

public class GetContentRestrictionProfilesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetContentRestrictionProfilesQuery, List<ContentRestrictionProfile>>
{
    public async Task<List<ContentRestrictionProfile>> Handle(GetContentRestrictionProfilesQuery request, CancellationToken cancellationToken)
    {
        return await context.ContentRestrictionProfiles
            .Include(p => p.Users)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
