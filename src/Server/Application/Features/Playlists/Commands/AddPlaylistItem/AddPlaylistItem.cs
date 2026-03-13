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
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        var maxOrder = await context.PlaylistItems
            .Where(i => i.PlaylistId == request.PlaylistId)
            .Select(i => (int?)i.Order)
            .MaxAsync(cancellationToken) ?? -1;

        var item = new PlaylistItem
        {
            PlaylistId = playlist.Id,
            MediaId = request.MediaId,
            Order = maxOrder + 1
        };

        context.PlaylistItems.Add(item);
        playlist.AddDomainEvent(new PlaylistItemAddedEvent(playlist, item));
        await context.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
