using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Collections;

namespace K7.Server.Application.Features.Collections.Queries.GetCollection;

public record GetCollectionQuery(Guid Id) : IRequest<Collection>;

public class GetCollectionQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetCollectionQuery, Collection>
{
    public async Task<Collection> Handle(GetCollectionQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var entity = await context.Collections
            .Include(c => c.CoverPicture)
                .ThenInclude(cp => cp!.Variants)
            .Include(c => c.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id && (c.IsPublic || c.UserId == userId), cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        return entity;
    }
}
