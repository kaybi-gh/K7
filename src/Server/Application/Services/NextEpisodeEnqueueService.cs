using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Services;

public interface INextEpisodeEnqueueService
{
    Task EnqueueNextEpisodeAsync(Guid userId, Guid episodeId, DateTime timeNow, CancellationToken cancellationToken = default);

    Task EnqueueNextEpisodeForSharedProfileAsync(
        Guid sharedProfileId,
        Guid episodeId,
        DateTime timeNow,
        CancellationToken cancellationToken = default);
}

public class NextEpisodeEnqueueService(IApplicationDbContext context) : INextEpisodeEnqueueService
{
    public async Task EnqueueNextEpisodeAsync(Guid userId, Guid episodeId, DateTime timeNow, CancellationToken cancellationToken = default)
    {
        var nextEpisodeId = await ResolveNextEpisodeIdAsync(episodeId, cancellationToken);
        if (nextEpisodeId is null)
            return;

        var nextState = await context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == nextEpisodeId.Value, cancellationToken);

        if (nextState is null)
        {
            nextState = new UserMediaState
            {
                UserId = userId,
                MediaId = nextEpisodeId.Value,
                PlayCount = 0,
                IsCompleted = false,
                LastPlaybackPosition = 0,
                ProgressPercentage = 0
            };
            context.UserMediaStates.Add(nextState);
        }
        else if (nextState.IsCompleted)
        {
            return;
        }

        // Keep Watching placeholder: LastInteractedAt only. Progress stays 0 so resume starts at 0:00.
        nextState.LastInteractedAt = timeNow;
        nextState.ExcludedFromContinueWatching = false;
    }

    public async Task EnqueueNextEpisodeForSharedProfileAsync(
        Guid sharedProfileId,
        Guid episodeId,
        DateTime timeNow,
        CancellationToken cancellationToken = default)
    {
        var nextEpisodeId = await ResolveNextEpisodeIdAsync(episodeId, cancellationToken);
        if (nextEpisodeId is null)
            return;

        var nextState = await context.SharedProfileMediaStates
            .FirstOrDefaultAsync(
                s => s.SharedProfileId == sharedProfileId && s.MediaId == nextEpisodeId.Value,
                cancellationToken);

        if (nextState is null)
        {
            nextState = new SharedProfileMediaState
            {
                SharedProfileId = sharedProfileId,
                MediaId = nextEpisodeId.Value,
                PlayCount = 0,
                IsCompleted = false,
                LastPlaybackPosition = 0,
                ProgressPercentage = 0
            };
            context.SharedProfileMediaStates.Add(nextState);
        }
        else if (nextState.IsCompleted)
        {
            return;
        }

        nextState.LastInteractedAt = timeNow;
        nextState.ExcludedFromContinueWatching = false;
    }

    private async Task<Guid?> ResolveNextEpisodeIdAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == episodeId, cancellationToken);

        if (episode is null)
            return null;

        var nextEpisode = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SeasonId == episode.SeasonId && e.EpisodeNumber > episode.EpisodeNumber)
            .OrderBy(e => e.EpisodeNumber)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextEpisode == default)
        {
            var currentSeasonNumber = await context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.Id == episode.Id)
                .Select(e => e.Season.SeasonNumber)
                .FirstOrDefaultAsync(cancellationToken);

            nextEpisode = await context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.SerieId == episode.SerieId && e.Season.SeasonNumber > currentSeasonNumber)
                .OrderBy(e => e.Season.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return nextEpisode == default ? null : nextEpisode;
    }
}
