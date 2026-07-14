using K7.Server.Application.Common.Behaviours;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;

public record AddPlaylistItemCommand : IRequest<Guid>, IMediaScopedRequest
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

        var media = await context.Medias
            .Where(m => m.Id == request.MediaId)
            .Select(m => new { m.Id, m.Type })
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        var maxOrder = await context.PlaylistItems
            .Where(i => i.PlaylistId == request.PlaylistId)
            .Select(i => (int?)i.Order)
            .MaxAsync(cancellationToken) ?? -1;

        PlaylistItem item;
        try
        {
            item = playlist.AddItem(media.Type, request.MediaId, maxOrder);
        }
        catch (InvalidOperationException ex)
        {
            throw new ValidationException(ex.Message);
        }

        context.PlaylistItems.Add(item);
        await context.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
