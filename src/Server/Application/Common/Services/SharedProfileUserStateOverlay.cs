using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

/// <summary>
/// Replaces personal <see cref="UserMediaState"/> graphs with shared-profile progress so
/// GetMedia / ToMediaDto expose the same resume state Keep Watching uses for the active group.
/// </summary>
public static class SharedProfileUserStateOverlay
{
    public static async Task ApplyAsync(
        IApplicationDbContext context,
        BaseMedia entity,
        Guid sharedProfileId,
        Guid actingUserId,
        CancellationToken cancellationToken = default)
    {
        var mediaIds = CollectGraphMediaIds(entity);
        if (mediaIds.Count == 0)
            return;

        var sharedStates = await context.SharedProfileMediaStates
            .AsNoTracking()
            .Where(s => s.SharedProfileId == sharedProfileId && mediaIds.Contains(s.MediaId))
            .ToDictionaryAsync(s => s.MediaId, cancellationToken);

        ApplyToGraph(entity, sharedStates, actingUserId);
    }

    internal static HashSet<Guid> CollectGraphMediaIds(BaseMedia entity)
    {
        var ids = new HashSet<Guid> { entity.Id };

        switch (entity)
        {
            case Serie serie:
                foreach (var season in serie.Seasons)
                {
                    ids.Add(season.Id);
                    foreach (var episode in season.Episodes)
                        ids.Add(episode.Id);
                }
                break;
            case SerieSeason season:
                foreach (var episode in season.Episodes)
                    ids.Add(episode.Id);
                break;
        }

        return ids;
    }

    private static void ApplyToGraph(
        BaseMedia entity,
        IReadOnlyDictionary<Guid, SharedProfileMediaState> sharedStates,
        Guid actingUserId)
    {
        ReplaceStates(entity, sharedStates, actingUserId);

        switch (entity)
        {
            case Serie serie:
                foreach (var season in serie.Seasons)
                {
                    ReplaceStates(season, sharedStates, actingUserId);
                    foreach (var episode in season.Episodes)
                        ReplaceStates(episode, sharedStates, actingUserId);
                }
                break;
            case SerieSeason season:
                foreach (var episode in season.Episodes)
                    ReplaceStates(episode, sharedStates, actingUserId);
                break;
        }
    }

    private static void ReplaceStates(
        BaseMedia media,
        IReadOnlyDictionary<Guid, SharedProfileMediaState> sharedStates,
        Guid actingUserId)
    {
        media.UserMediaStates.Clear();
        if (sharedStates.TryGetValue(media.Id, out var shared))
            media.UserMediaStates.Add(shared.ToUserMediaState(actingUserId));
    }
}
