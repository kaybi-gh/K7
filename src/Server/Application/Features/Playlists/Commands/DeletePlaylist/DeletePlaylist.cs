using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.DeletePlaylist;

public record DeletePlaylistCommand(Guid Id) : IRequest;

public class DeletePlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<DeletePlaylistCommand>
{
    public async Task Handle(DeletePlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        context.Playlists.Remove(entity);
        entity.AddDomainEvent(new PlaylistDeletedEvent(entity));
        await context.SaveChangesAsync(cancellationToken);
    }
}
