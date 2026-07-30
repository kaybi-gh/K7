using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.DynamicPlaylists.Queries.GetDynamicPlaylist;

public record GetDynamicPlaylistQuery(Guid Id) : IRequest<DynamicPlaylist>;

public class GetDynamicPlaylistQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetDynamicPlaylistQuery, DynamicPlaylist>
{
    public async Task<DynamicPlaylist> Handle(GetDynamicPlaylistQuery request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists.OfType<DynamicPlaylist>()
            .Include(p => p.UserStates.Where(s => s.UserId == currentUser.Id!.Value))
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        return entity;
    }
}
