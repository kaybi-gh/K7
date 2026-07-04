using K7.Server.Application.Common.Extensions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Playlists.Queries.GetPlaylists;

public record GetPlaylistsWithPaginationQuery : IRequest<PaginatedList<Playlist>>
{
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 20;
    public MediaType? MediaType { get; init; }
    public LibraryItemOrderingOption? OrderBy { get; init; }
}

public class GetPlaylistsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetPlaylistsWithPaginationQuery, PaginatedList<Playlist>>
{
    public async Task<PaginatedList<Playlist>> Handle(GetPlaylistsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return new PaginatedList<Playlist>([], 0, request.PageNumber, request.PageSize);

        var query = context.Playlists
            .Include(p => p.UserStates.Where(s => s.UserId == userId))
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .Include(p => p.Items)
                .ThenInclude(i => i.Media)
                    .ThenInclude(m => m.Pictures)
                        .ThenInclude(p => p.Variants)
            .Include(p => p.Items)
                .ThenInclude(i => (i.Media as MusicTrack)!.Album)
                    .ThenInclude(a => a!.Pictures)
                        .ThenInclude(p => p.Variants)
            .Where(p => p.UserId == userId)
            .AsQueryable();

        if (request.MediaType.HasValue)
            query = query.Where(p => p.MediaType == request.MediaType.Value);

        query = query
            .ApplyOrdering(request.OrderBy, userId)
            .AsNoTracking();

        return await query.PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
