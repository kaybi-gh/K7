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

    /// <summary>
    /// After a new episode becomes available, place it in Keep Watching for viewers who
    /// finished the previous episode (weekly releases, late-arriving files).
    /// Inherits LastInteractedAt from that previous watch so the max-age window still applies.
    /// Existing states are left untouched (real progress, dismiss, already-enqueued placeholders).
    /// </summary>
    Task EnqueueWatchersForNewEpisodeAsync(Guid episodeId, CancellationToken cancellationToken = default);
}

public class NextEpisodeEnqueueService(IApplicationDbContext context) : INextEpisodeEnqueueService
{
    public async Task EnqueueNextEpisodeAsync(Guid userId, Guid episodeId, DateTime timeNow, CancellationToken cancellationToken = default)
    {
        var nextEpisodeId = await ResolveNextUnwatchedEpisodeIdAsync(
            episodeId,
            mediaId => context.UserMediaStates
                .Where(s => s.UserId == userId && s.MediaId == mediaId)
                .Select(s => (bool?)s.IsCompleted)
                .FirstOrDefaultAsync(cancellationToken),
            cancellationToken);
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
        var nextEpisodeId = await ResolveNextUnwatchedEpisodeIdAsync(
            episodeId,
            mediaId => context.SharedProfileMediaStates
                .Where(s => s.SharedProfileId == sharedProfileId && s.MediaId == mediaId)
                .Select(s => (bool?)s.IsCompleted)
                .FirstOrDefaultAsync(cancellationToken),
            cancellationToken);
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

    public async Task EnqueueWatchersForNewEpisodeAsync(Guid episodeId, CancellationToken cancellationToken = default)
    {
        var previousEpisodeId = await ResolvePreviousEpisodeIdAsync(episodeId, cancellationToken);
        if (previousEpisodeId is null)
            return;

        var completedUsers = await context.UserMediaStates
            .AsNoTracking()
            .Where(s => s.MediaId == previousEpisodeId.Value && s.IsCompleted && s.LastInteractedAt != null)
            .Select(s => new { s.UserId, LastInteractedAt = s.LastInteractedAt!.Value })
            .ToListAsync(cancellationToken);

        if (completedUsers.Count > 0)
        {
            var userIds = completedUsers.Select(u => u.UserId).ToList();
            var existingUserIds = await context.UserMediaStates
                .Where(s => s.MediaId == episodeId && userIds.Contains(s.UserId))
                .Select(s => s.UserId)
                .ToListAsync(cancellationToken);
            var existingSet = existingUserIds.ToHashSet();

            foreach (var user in completedUsers)
            {
                if (existingSet.Contains(user.UserId))
                    continue;

                context.UserMediaStates.Add(new UserMediaState
                {
                    UserId = user.UserId,
                    MediaId = episodeId,
                    PlayCount = 0,
                    IsCompleted = false,
                    LastPlaybackPosition = 0,
                    ProgressPercentage = 0,
                    LastInteractedAt = user.LastInteractedAt,
                    ExcludedFromContinueWatching = false
                });
            }
        }

        var completedProfiles = await context.SharedProfileMediaStates
            .AsNoTracking()
            .Where(s => s.MediaId == previousEpisodeId.Value && s.IsCompleted && s.LastInteractedAt != null)
            .Select(s => new { s.SharedProfileId, LastInteractedAt = s.LastInteractedAt!.Value })
            .ToListAsync(cancellationToken);

        if (completedProfiles.Count == 0)
            return;

        var profileIds = completedProfiles.Select(p => p.SharedProfileId).ToList();
        var existingProfileIds = await context.SharedProfileMediaStates
            .Where(s => s.MediaId == episodeId && profileIds.Contains(s.SharedProfileId))
            .Select(s => s.SharedProfileId)
            .ToListAsync(cancellationToken);
        var existingProfileSet = existingProfileIds.ToHashSet();

        foreach (var profile in completedProfiles)
        {
            if (existingProfileSet.Contains(profile.SharedProfileId))
                continue;

            context.SharedProfileMediaStates.Add(new SharedProfileMediaState
            {
                SharedProfileId = profile.SharedProfileId,
                MediaId = episodeId,
                PlayCount = 0,
                IsCompleted = false,
                LastPlaybackPosition = 0,
                ProgressPercentage = 0,
                LastInteractedAt = profile.LastInteractedAt,
                ExcludedFromContinueWatching = false
            });
        }
    }

    private async Task<Guid?> ResolvePreviousEpisodeIdAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => e.Id == episodeId)
            .Select(e => new { e.SeasonId, e.SerieId, e.EpisodeNumber, e.Season.SeasonNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
            return null;

        var previousInSeason = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SeasonId == episode.SeasonId && e.EpisodeNumber < episode.EpisodeNumber)
            .OrderByDescending(e => e.EpisodeNumber)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousInSeason != default)
            return previousInSeason;

        var previousSeasonLast = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SerieId == episode.SerieId && e.Season.SeasonNumber < episode.SeasonNumber)
            .OrderByDescending(e => e.Season.SeasonNumber)
            .ThenByDescending(e => e.EpisodeNumber)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return previousSeasonLast == default ? null : previousSeasonLast;
    }

    private async Task<Guid?> ResolveNextUnwatchedEpisodeIdAsync(
        Guid episodeId,
        Func<Guid, Task<bool?>> getIsCompletedAsync,
        CancellationToken cancellationToken)
    {
        var currentId = episodeId;
        for (var i = 0; i < 500; i++)
        {
            var nextId = await ResolveNextEpisodeIdAsync(currentId, cancellationToken);
            if (nextId is null)
                return null;

            var isCompleted = await getIsCompletedAsync(nextId.Value);
            if (isCompleted != true)
                return nextId;

            currentId = nextId.Value;
        }

        return null;
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
