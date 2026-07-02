using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.Playlists.Queries.GetPlaylist;

public record GetPlaylistQuery(Guid Id) : IRequest<Playlist>;

public class GetPlaylistQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetPlaylistQuery, Playlist>
{
    public async Task<Playlist> Handle(GetPlaylistQuery request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists
            .Include(p => p.UserStates.Where(s => s.UserId == currentUser.Id!.Value))
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .Include(p => p.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        return entity;
    }
}
