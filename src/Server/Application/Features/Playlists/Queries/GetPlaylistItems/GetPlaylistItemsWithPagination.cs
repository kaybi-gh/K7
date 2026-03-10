using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.Playlists.Queries.GetPlaylistItems;

public record GetPlaylistItemsWithPaginationQuery : IRequest<PaginatedList<PlaylistItem>>
{
    public required Guid PlaylistId { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 50;
}

public class GetPlaylistItemsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetPlaylistItemsWithPaginationQuery, PaginatedList<PlaylistItem>>
{
    public async Task<PaginatedList<PlaylistItem>> Handle(GetPlaylistItemsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        var query = context.PlaylistItems
            .Include(i => i.Media)
                .ThenInclude(m => m.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(i => i.Media)
                .ThenInclude(m => m.IndexedFiles)
            .Include(i => i.Media)
                .ThenInclude(m => m.PersonRoles)
                    .ThenInclude(r => r.Person)
            .Where(i => i.PlaylistId == request.PlaylistId)
            .OrderBy(i => i.Order)
            .AsNoTracking();

        return await query.PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
