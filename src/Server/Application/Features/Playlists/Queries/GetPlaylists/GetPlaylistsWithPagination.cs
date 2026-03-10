using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.Playlists.Queries.GetPlaylists;

public record GetPlaylistsWithPaginationQuery : IRequest<PaginatedList<Playlist>>
{
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 20;
}

public class GetPlaylistsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetPlaylistsWithPaginationQuery, PaginatedList<Playlist>>
{
    public async Task<PaginatedList<Playlist>> Handle(GetPlaylistsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = context.Playlists
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .Include(p => p.Items)
            .Where(p => p.UserId == currentUser.Id!.Value)
            .OrderByDescending(p => p.LastModified)
            .AsNoTracking();

        return await query.PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
