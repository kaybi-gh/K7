using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Features.Collections.Queries.GetCollectionItems;

public record GetCollectionItemsWithPaginationQuery : IRequest<PaginatedList<CollectionItem>>
{
    public required Guid CollectionId { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.ItemsPageSize;
}

public class GetCollectionItemsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetCollectionItemsWithPaginationQuery, PaginatedList<CollectionItem>>
{
    public async Task<PaginatedList<CollectionItem>> Handle(GetCollectionItemsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var collection = await context.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId && (c.IsPublic || c.UserId == userId), cancellationToken);

        Guard.Against.NotFound(request.CollectionId, collection);

        var query = context.CollectionItems
            .Include(i => i.Media)
                .ThenInclude(m => m.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(i => i.Media)
                .ThenInclude(m => m.PersonRoles)
                    .ThenInclude(r => r.Person)
            .Include(i => i.Media)
                .ThenInclude(m => m.Ratings)
            .Include(i => (i.Media as MusicAlbum)!.Tracks)
            .Where(i => i.CollectionId == request.CollectionId)
            .OrderBy(i => i.Order)
            .AsSplitQuery()
            .AsNoTracking();

        return await query.PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
