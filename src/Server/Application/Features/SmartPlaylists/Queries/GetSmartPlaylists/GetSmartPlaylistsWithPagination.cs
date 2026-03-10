using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.SmartPlaylists.Queries.GetSmartPlaylists;

public record GetSmartPlaylistsWithPaginationQuery : IRequest<PaginatedList<SmartPlaylist>>
{
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 20;
}

public class GetSmartPlaylistsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetSmartPlaylistsWithPaginationQuery, PaginatedList<SmartPlaylist>>
{
    public async Task<PaginatedList<SmartPlaylist>> Handle(GetSmartPlaylistsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = context.Playlists.OfType<SmartPlaylist>()
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .Where(p => p.UserId == currentUser.Id!.Value)
            .OrderByDescending(p => p.LastModified)
            .AsNoTracking();

        return await query.PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
