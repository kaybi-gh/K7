using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;

public record AddPlaylistItemCommand : IRequest<Guid>
{
    public required Guid PlaylistId { get; init; }
    public required Guid MediaId { get; init; }
}

public class AddPlaylistItemCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<AddPlaylistItemCommand, Guid>
{
    public async Task<Guid> Handle(AddPlaylistItemCommand request, CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        var maxOrder = playlist.Items.Count > 0 ? playlist.Items.Max(i => i.Order) : -1;

        var item = new PlaylistItem
        {
            Id = Guid.NewGuid(),
            PlaylistId = playlist.Id,
            MediaId = request.MediaId,
            Order = maxOrder + 1
        };

        playlist.Items.Add(item);
        playlist.AddDomainEvent(new PlaylistItemAddedEvent(playlist, item));
        await context.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
