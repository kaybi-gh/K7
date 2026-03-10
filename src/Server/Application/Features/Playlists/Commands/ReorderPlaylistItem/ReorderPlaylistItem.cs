using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.ReorderPlaylistItem;

public record ReorderPlaylistItemCommand : IRequest
{
    public required Guid PlaylistId { get; init; }
    public required Guid ItemId { get; init; }
    public required int NewOrder { get; init; }
}

public class ReorderPlaylistItemCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<ReorderPlaylistItemCommand>
{
    public async Task Handle(ReorderPlaylistItemCommand request, CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        var item = playlist.Items.FirstOrDefault(i => i.Id == request.ItemId);
        Guard.Against.NotFound(request.ItemId, item);

        var newOrder = Math.Clamp(request.NewOrder, 0, playlist.Items.Count - 1);
        var oldOrder = item.Order;

        if (oldOrder == newOrder)
            return;

        foreach (var other in playlist.Items.Where(i => i.Id != item.Id))
        {
            if (oldOrder < newOrder && other.Order > oldOrder && other.Order <= newOrder)
                other.Order--;
            else if (oldOrder > newOrder && other.Order >= newOrder && other.Order < oldOrder)
                other.Order++;
        }

        item.Order = newOrder;

        playlist.AddDomainEvent(new PlaylistUpdatedEvent(playlist));
        await context.SaveChangesAsync(cancellationToken);
    }
}
