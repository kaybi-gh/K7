using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.DynamicPlaylists.Queries.GetDynamicPlaylist;

public record GetDynamicPlaylistQuery(Guid Id) : IRequest<DynamicPlaylistDetails>;

/// <summary>
/// A dynamic playlist with its item count. Items are not loaded: a playlist can hold
/// thousands of them and the detail page pages through them separately.
/// </summary>
public record DynamicPlaylistDetails(DynamicPlaylist Playlist, int ItemCount);

public class GetDynamicPlaylistQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetDynamicPlaylistQuery, DynamicPlaylistDetails>
{
    public async Task<DynamicPlaylistDetails> Handle(GetDynamicPlaylistQuery request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists.OfType<DynamicPlaylist>()
            .Include(p => p.UserStates.Where(s => s.UserId == currentUser.Id!.Value))
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var itemCount = await context.PlaylistItems
            .AsNoTracking()
            .CountAsync(i => i.PlaylistId == entity.Id, cancellationToken);

        return new DynamicPlaylistDetails(entity, itemCount);
    }
}
