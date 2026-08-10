using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Deletes serie episodes that no longer have files and carry no user data.
/// </summary>
public static class SerieEpisodeOrphanCleanupHelper
{
    /// <summary>
    /// Removes <paramref name="episodeId"/> when it has no local/remote files and no watch/review/playlist state.
    /// Also removes the parent season when it would be left with zero episodes.
    /// </summary>
    /// <returns><see langword="true"/> when the episode was deleted.</returns>
    public static async Task<bool> TryDeleteIfOrphanAsync(
        IApplicationDbContext context,
        Guid episodeId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .Include(e => e.IndexedFiles)
            .Include(e => e.RemoteIndexedFiles)
            .Include(e => e.Season)
            .FirstOrDefaultAsync(e => e.Id == episodeId, cancellationToken);

        if (episode is null)
            return false;

        if (episode.IndexedFiles.Count > 0 || episode.RemoteIndexedFiles.Count > 0)
            return false;

        if (await MediaHasUserDataHelper.HasUserDataAsync(context, episodeId, cancellationToken))
        {
            logger.LogInformation(
                "Keeping orphan episode {EpisodeId} (S{SeasonNumber}E{EpisodeNumber}) because user data exists",
                episodeId,
                episode.Season.SeasonNumber,
                episode.EpisodeNumber);
            return false;
        }

        var seasonId = episode.SeasonId;
        context.Medias.Remove(episode);

        if (await CountRemainingEpisodesAsync(context, seasonId, episodeId, cancellationToken) == 0)
        {
            var season = await context.Medias
                .OfType<SerieSeason>()
                .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);
            if (season is not null)
                context.Medias.Remove(season);
        }

        logger.LogInformation(
            "Deleted orphan episode {EpisodeId} with no files and no user data",
            episodeId);

        return true;
    }

    /// <summary>
    /// Counts sibling episodes on the season, adjusting for entities Added/Deleted in the change tracker
    /// that are not yet reflected in the database.
    /// </summary>
    private static async Task<int> CountRemainingEpisodesAsync(
        IApplicationDbContext context,
        Guid seasonId,
        Guid excludingEpisodeId,
        CancellationToken cancellationToken)
    {
        var count = await context.Medias
            .OfType<SerieEpisode>()
            .CountAsync(e => e.SeasonId == seasonId && e.Id != excludingEpisodeId, cancellationToken);

        foreach (var tracked in context.Medias.Local.OfType<SerieEpisode>())
        {
            if (tracked.Id == excludingEpisodeId || tracked.SeasonId != seasonId)
                continue;

            var state = context.Entry(tracked).State;
            if (state == EntityState.Added)
                count++;
            else if (state == EntityState.Deleted)
                count--;
        }

        return count;
    }
}
