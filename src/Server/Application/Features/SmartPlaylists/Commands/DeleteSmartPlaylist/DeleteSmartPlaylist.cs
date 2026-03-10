using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.SmartPlaylists.Commands.DeleteSmartPlaylist;

public record DeleteSmartPlaylistCommand(Guid Id) : IRequest;

public class DeleteSmartPlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<DeleteSmartPlaylistCommand>
{
    public async Task Handle(DeleteSmartPlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists.OfType<SmartPlaylist>()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        context.Playlists.Remove(entity);
        entity.AddDomainEvent(new SmartPlaylistDeletedEvent(entity));
        await context.SaveChangesAsync(cancellationToken);
    }
}
