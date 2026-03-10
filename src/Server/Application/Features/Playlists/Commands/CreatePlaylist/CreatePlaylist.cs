using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;

public record CreatePlaylistCommand : IRequest<Guid>
{
    public required string Title { get; init; }
    public string? Description { get; init; }
}

public class CreatePlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<CreatePlaylistCommand, Guid>
{
    public async Task<Guid> Handle(CreatePlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = new Playlist
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            UserId = currentUser.Id!.Value
        };

        entity.AddDomainEvent(new PlaylistCreatedEvent(entity));
        context.Playlists.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
