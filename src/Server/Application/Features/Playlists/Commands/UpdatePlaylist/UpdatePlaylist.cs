using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;

public record UpdatePlaylistCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
}

public class UpdatePlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<UpdatePlaylistCommand>
{
    public async Task Handle(UpdatePlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.Title = request.Title;
        entity.Description = request.Description;

        entity.AddDomainEvent(new PlaylistUpdatedEvent(entity));
        await context.SaveChangesAsync(cancellationToken);
    }
}
