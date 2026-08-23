using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Deletes serie episodes that no longer have files and carry no user data.
/// Cascades to empty seasons and empty series (same pattern as music track -> album -> artist).
/// </summary>
public static class SerieEpisodeOrphanCleanupHelper
{
    /// <summary>
    /// Removes <paramref name="episodeId"/> when it has no local/remote files and no watch/review/playlist state.
    /// Also removes the parent season when it would be left with zero episodes, and the parent serie when
    /// it would be left with zero seasons and no user data.
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

        if (episode.IndexedFiles.Count > 0
            || episode.RemoteIndexedFiles.Count > 0
            || await MediaOrphanDependentCleanupHelper.HasRemainingFilesAsync(context, episodeId, cancellationToken))
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
        var serieId = episode.SerieId;
        await MediaOrphanDependentCleanupHelper.ClearNonUserDependentsAsync(context, episodeId, cancellationToken);
        context.Medias.Remove(episode);

        if (await CountRemainingEpisodesAsync(context, seasonId, episodeId, cancellationToken) == 0)
        {
            var season = await context.Medias
                .OfType<SerieSeason>()
                .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);
            if (season is not null)
            {
                context.Medias.Remove(season);

                if (await CountRemainingSeasonsAsync(context, serieId, seasonId, cancellationToken) == 0)
                {
                    await TryDeleteSerieIfOrphanAsync(
                        context,
                        serieId,
                        logger,
                        excludingSeasonId: seasonId,
                        cancellationToken);
                }
            }
        }

        logger.LogInformation(
            "Deleted orphan episode {EpisodeId} with no files and no user data",
            episodeId);

        return true;
    }

    /// <summary>
    /// Removes <paramref name="serieId"/> when it has no seasons left and no user data.
    /// </summary>
    public static async Task<bool> TryDeleteSerieIfOrphanAsync(
        IApplicationDbContext context,
        Guid serieId,
        ILogger logger,
        Guid? excludingSeasonId = null,
        CancellationToken cancellationToken = default)
    {
        var serie = await context.Medias
            .OfType<Serie>()
            .FirstOrDefaultAsync(s => s.Id == serieId, cancellationToken);

        if (serie is null)
            return false;

        if (await CountRemainingSeasonsAsync(context, serieId, excludingSeasonId, cancellationToken) > 0)
            return false;

        if (await MediaHasUserDataHelper.HasUserDataAsync(context, serieId, cancellationToken))
        {
            logger.LogInformation(
                "Keeping orphan serie {SerieId} ({Title}) because user data exists",
                serieId,
                serie.Title);
            return false;
        }

        await MediaOrphanDependentCleanupHelper.ClearNonUserDependentsAsync(context, serieId, cancellationToken);
        context.Medias.Remove(serie);
        logger.LogInformation(
            "Deleted orphan serie {SerieId} with no seasons and no user data",
            serieId);
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

    private static async Task<int> CountRemainingSeasonsAsync(
        IApplicationDbContext context,
        Guid serieId,
        Guid? excludingSeasonId,
        CancellationToken cancellationToken)
    {
        var query = context.Medias.OfType<SerieSeason>().Where(s => s.SerieId == serieId);
        if (excludingSeasonId is Guid excludeId)
            query = query.Where(s => s.Id != excludeId);

        var count = await query.CountAsync(cancellationToken);

        foreach (var tracked in context.Medias.Local.OfType<SerieSeason>())
        {
            if (tracked.SerieId != serieId)
                continue;
            if (excludingSeasonId is Guid exclude && tracked.Id == exclude)
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
