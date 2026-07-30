using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.DynamicPlaylists.Commands.DeleteDynamicPlaylist;

public record DeleteDynamicPlaylistCommand(Guid Id) : IRequest;

public class DeleteDynamicPlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<DeleteDynamicPlaylistCommand>
{
    public async Task Handle(DeleteDynamicPlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists.OfType<DynamicPlaylist>()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        context.Playlists.Remove(entity);
        entity.AddDomainEvent(new DynamicPlaylistDeletedEvent(entity));
        await context.SaveChangesAsync(cancellationToken);
    }
}
