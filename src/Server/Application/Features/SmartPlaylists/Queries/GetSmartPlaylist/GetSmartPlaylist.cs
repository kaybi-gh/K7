using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.SmartPlaylists.Queries.GetSmartPlaylist;

public record GetSmartPlaylistQuery(Guid Id) : IRequest<SmartPlaylist>;

public class GetSmartPlaylistQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetSmartPlaylistQuery, SmartPlaylist>
{
    public async Task<SmartPlaylist> Handle(GetSmartPlaylistQuery request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists.OfType<SmartPlaylist>()
            .Include(p => p.UserStates.Where(s => s.UserId == currentUser.Id!.Value))
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        return entity;
    }
}
