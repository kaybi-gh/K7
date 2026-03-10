using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.SmartPlaylists.Services;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.SmartPlaylists.Commands.EvaluateSmartPlaylist;

public record EvaluateSmartPlaylistCommand : IRequest<Guid>
{
    public required Guid Id { get; init; }
}

public class EvaluateSmartPlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<EvaluateSmartPlaylistCommand, Guid>
{
    public async Task<Guid> Handle(EvaluateSmartPlaylistCommand request, CancellationToken cancellationToken)
    {
        var smartPlaylist = await context.Playlists.OfType<SmartPlaylist>()
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, smartPlaylist);

        var userId = currentUser.Id!.Value;

        var query = context.Medias
            .Where(m => m.IndexedFiles.Any())
            .AsNoTracking();

        query = SmartPlaylistEvaluator.ApplyRules(query, smartPlaylist, userId);

        var mediaIds = await query.Select(m => m.Id).ToListAsync(cancellationToken);

        smartPlaylist.Items.Clear();

        for (var i = 0; i < mediaIds.Count; i++)
        {
            smartPlaylist.Items.Add(new PlaylistItem
            {
                MediaId = mediaIds[i],
                Order = i
            });
        }

        smartPlaylist.LastEvaluatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return smartPlaylist.Id;
    }
}
