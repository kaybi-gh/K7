using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.DynamicPlaylists.Queries.GetDynamicPlaylists;

public record GetDynamicPlaylistsWithPaginationQuery : IRequest<PaginatedList<DynamicPlaylist>>
{
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
}

public class GetDynamicPlaylistsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetDynamicPlaylistsWithPaginationQuery, PaginatedList<DynamicPlaylist>>
{
    public async Task<PaginatedList<DynamicPlaylist>> Handle(GetDynamicPlaylistsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return new PaginatedList<DynamicPlaylist>([], 0, request.PageNumber, request.PageSize);

        var query = context.Playlists.OfType<DynamicPlaylist>()
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.LastModified)
            .AsNoTracking();

        return await query.PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
