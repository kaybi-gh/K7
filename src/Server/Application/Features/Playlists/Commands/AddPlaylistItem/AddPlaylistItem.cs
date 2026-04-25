using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;

public record AddPlaylistItemCommand : IRequest<Guid>
{
    public required Guid PlaylistId { get; init; }
    public required Guid MediaId { get; init; }
}

public class AddPlaylistItemCommandHandler(IApplicationDbContext context, IUser currentUser, IMediaAccessGuard accessGuard)
    : IRequestHandler<AddPlaylistItemCommand, Guid>
{
    public async Task<Guid> Handle(AddPlaylistItemCommand request, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);
        var playlist = await context.Playlists
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        var media = await context.Medias
            .Where(m => m.Id == request.MediaId)
            .Select(m => new { m.Id, m.Type })
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        if (media.Type != playlist.MediaType)
            throw new ValidationException($"Cannot add a media of type {media.Type} to a playlist of type {playlist.MediaType}.");

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
