using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Services;

public interface IContinueWatchingExclusionService
{
    Task DismissAsync(Guid userId, Guid mediaId, CancellationToken cancellationToken = default);

    Task ClearExclusionCascadeAsync(
        Guid userId,
        BaseMedia media,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

public class ContinueWatchingExclusionService(
    IApplicationDbContext context,
    IPlaybackPolicySettingsProvider policyProvider) : IContinueWatchingExclusionService
{
    public async Task DismissAsync(Guid userId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var policy = await policyProvider.GetEffectiveVideoPolicyAsync(userId, cancellationToken);
        var utcNow = DateTime.UtcNow;
        var cutoff = ContinueWatchingEligibility.GetWindowCutoff(policy, utcNow);

        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        if (media is SerieEpisode episode)
        {
            await ExcludeSerieStatesAsync(userId, episode.SerieId, cutoff, cancellationToken);
            return;
        }

        await ExcludeSingleMediaAsync(userId, mediaId, cancellationToken);
    }

    public async Task ClearExclusionCascadeAsync(
        Guid userId,
        BaseMedia media,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (media is not SerieEpisode)
            return;

        var currentEpisode = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.Id == media.Id)
            .Select(e => new EpisodeSortInfo(e.Id, e.SerieId, e.Season.SeasonNumber, e.EpisodeNumber))
            .FirstOrDefaultAsync(cancellationToken);

        if (currentEpisode is null)
            return;

        var cutoff = ContinueWatchingEligibility.GetWindowCutoff(policy, utcNow);
        var currentSortKey = ContinueWatchingEpisodeOrder.GetSortKey(
            currentEpisode.SeasonNumber,
            currentEpisode.EpisodeNumber);

        var episodes = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SerieId == currentEpisode.SerieId)
            .Select(e => new EpisodeSortInfo(e.Id, e.SerieId, e.Season.SeasonNumber, e.EpisodeNumber))
            .ToListAsync(cancellationToken);

        var targetMediaIds = episodes
            .Where(e => ContinueWatchingEpisodeOrder.GetSortKey(e.SeasonNumber, e.EpisodeNumber) >= currentSortKey)
            .Select(e => e.Id)
            .ToList();

        if (targetMediaIds.Count == 0)
            return;

        var states = await context.UserMediaStates
            .Where(s => s.UserId == userId
                && targetMediaIds.Contains(s.MediaId)
                && s.ExcludedFromContinueWatching
                && (cutoff == null || s.LastInteractedAt >= cutoff))
            .ToListAsync(cancellationToken);

        foreach (var state in states)
            state.ExcludedFromContinueWatching = false;
    }

    private async Task ExcludeSerieStatesAsync(
        Guid userId,
        Guid serieId,
        DateTime? cutoff,
        CancellationToken cancellationToken)
    {
        var episodeIds = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SerieId == serieId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (episodeIds.Count == 0)
            return;

        var states = await context.UserMediaStates
            .Where(s => s.UserId == userId
                && episodeIds.Contains(s.MediaId)
                && !s.IsCompleted
                && (cutoff == null || s.LastInteractedAt >= cutoff))
            .ToListAsync(cancellationToken);

        foreach (var state in states)
            state.ExcludedFromContinueWatching = true;
    }

    private async Task ExcludeSingleMediaAsync(
        Guid userId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var state = await context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == mediaId, cancellationToken);

        if (state is null)
        {
            state = new UserMediaState
            {
                UserId = userId,
                MediaId = mediaId,
                ExcludedFromContinueWatching = true,
                LastInteractedAt = DateTime.UtcNow
            };
            context.UserMediaStates.Add(state);
            return;
        }

        state.ExcludedFromContinueWatching = true;
    }

    private sealed record EpisodeSortInfo(Guid Id, Guid SerieId, int SeasonNumber, int EpisodeNumber);
}
