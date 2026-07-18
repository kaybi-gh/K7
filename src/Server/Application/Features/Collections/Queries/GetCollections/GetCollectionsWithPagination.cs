using K7.Server.Application.Common.Extensions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Collections.Queries.GetCollections;

public record GetCollectionsWithPaginationQuery : IRequest<PaginatedList<Collection>>
{
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
    public MediaType? MediaType { get; init; }
    public bool? IsPublic { get; init; }
    public LibraryItemOrderingOption? OrderBy { get; init; }
}

public class GetCollectionsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetCollectionsWithPaginationQuery, PaginatedList<Collection>>
{
    public async Task<PaginatedList<Collection>> Handle(GetCollectionsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var query = context.Collections
            .Include(c => c.CoverPicture)
                .ThenInclude(cp => cp!.Variants)
            .Include(c => c.Items)
                .ThenInclude(i => i.Media)
                    .ThenInclude(m => m.Pictures)
                        .ThenInclude(p => p.Variants)
            .Include(c => c.Items)
                .ThenInclude(i => (i.Media as MusicTrack)!.Album)
                    .ThenInclude(a => a!.Pictures)
                        .ThenInclude(p => p.Variants)
            .Where(c => c.IsPublic || c.UserId == userId)
            .AsQueryable();

        if (request.MediaType.HasValue)
            query = query.Where(c => c.MediaType == request.MediaType.Value || c.MediaType == null);

        if (request.IsPublic.HasValue)
            query = query.Where(c => c.IsPublic == request.IsPublic.Value);

        query = query
            .ApplyOrdering(request.OrderBy)
            .AsSplitQuery()
            .AsNoTracking();

        return await query.PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
