using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Services;

public interface INextEpisodeEnqueueService
{
    Task EnqueueNextEpisodeAsync(Guid userId, Guid episodeId, DateTime timeNow, CancellationToken cancellationToken = default);
}

public class NextEpisodeEnqueueService(IApplicationDbContext context) : INextEpisodeEnqueueService
{
    public async Task EnqueueNextEpisodeAsync(Guid userId, Guid episodeId, DateTime timeNow, CancellationToken cancellationToken = default)
    {
        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == episodeId, cancellationToken);

        if (episode is null)
            return;

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

        if (nextEpisode == default)
            return;

        var nextState = await context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == nextEpisode, cancellationToken);

        if (nextState is null)
        {
            nextState = new UserMediaState
            {
                UserId = userId,
                MediaId = nextEpisode,
                PlayCount = 0,
                IsCompleted = false,
                LastPlaybackPosition = 0
            };
            context.UserMediaStates.Add(nextState);
        }
        else if (nextState.IsCompleted)
        {
            return;
        }

        nextState.LastInteractedAt = timeNow;
    }
}
