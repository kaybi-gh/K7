using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.RemovePlaylistItem;

public record RemovePlaylistItemCommand : IRequest
{
    public required Guid PlaylistId { get; init; }
    public required Guid ItemId { get; init; }
}

public class RemovePlaylistItemCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<RemovePlaylistItemCommand>
{
    public async Task Handle(RemovePlaylistItemCommand request, CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        var item = playlist.Items.FirstOrDefault(i => i.Id == request.ItemId);
        Guard.Against.NotFound(request.ItemId, item);

        playlist.Items.Remove(item);
        playlist.AddDomainEvent(new PlaylistItemRemovedEvent(playlist, item));

        // Reorder remaining items
        var remaining = playlist.Items.OrderBy(i => i.Order).ToList();
        for (var i = 0; i < remaining.Count; i++)
            remaining[i].Order = i;

        await context.SaveChangesAsync(cancellationToken);
    }
}
