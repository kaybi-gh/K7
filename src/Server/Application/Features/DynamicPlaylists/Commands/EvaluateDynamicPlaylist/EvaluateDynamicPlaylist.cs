using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.DynamicPlaylists.Services;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.DynamicPlaylists.Commands.EvaluateDynamicPlaylist;

public record EvaluateDynamicPlaylistCommand : IRequest<Guid>
{
    public required Guid Id { get; init; }
}

public class EvaluateDynamicPlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<EvaluateDynamicPlaylistCommand, Guid>
{
    public async Task<Guid> Handle(EvaluateDynamicPlaylistCommand request, CancellationToken cancellationToken)
    {
        var dynamicPlaylist = await context.Playlists.OfType<DynamicPlaylist>()
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, dynamicPlaylist);

        var userId = currentUser.Id!.Value;

        var query = context.Medias
            .Where(m => m.IndexedFiles.Any())
            .AsNoTracking();

        query = DynamicPlaylistEvaluator.ApplyRules(query, dynamicPlaylist, userId);

        var mediaIds = await query.Select(m => m.Id).ToListAsync(cancellationToken);

        dynamicPlaylist.Items.Clear();

        for (var i = 0; i < mediaIds.Count; i++)
        {
            dynamicPlaylist.Items.Add(new PlaylistItem
            {
                MediaId = mediaIds[i],
                Order = i
            });
        }

        dynamicPlaylist.LastEvaluatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return dynamicPlaylist.Id;
    }
}
