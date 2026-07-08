using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Services;

public sealed record UserMediaStateUpdateResult(
    double ProgressPercentage,
    bool IsCompleted,
    bool WasNewlyCompleted,
    Guid? EpisodeIdForEnqueue);

public interface IUserMediaStateUpdater
{
    Task<UserMediaStateUpdateResult> ApplyAsync(
        Guid userId,
        BaseMedia media,
        Guid mediaId,
        double position,
        double duration,
        DateTime timeNow,
        CancellationToken cancellationToken = default);
}

public class UserMediaStateUpdater(
    IApplicationDbContext context,
    IPlaybackPolicySettingsProvider policyProvider,
    IContinueWatchingExclusionService exclusionService) : IUserMediaStateUpdater
{
    public async Task<UserMediaStateUpdateResult> ApplyAsync(
        Guid userId,
        BaseMedia media,
        Guid mediaId,
        double position,
        double duration,
        DateTime timeNow,
        CancellationToken cancellationToken = default)
    {
        var state = await context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == mediaId, cancellationToken);

        if (state is null)
        {
            state = new UserMediaState
            {
                UserId = userId,
                MediaId = mediaId,
                PlayCount = 0,
                IsCompleted = false,
                LastPlaybackPosition = 0
            };
            context.UserMediaStates.Add(state);
        }

        state.LastInteractedAt = timeNow;
        state.LastKnownDurationSeconds = duration;

        var progress = duration > 0 ? position / duration : 0;
        var isMusic = media.Type == MediaType.MusicTrack;

        var videoPolicy = await policyProvider.GetEffectiveVideoPolicyAsync(userId, cancellationToken);
        var audioPolicy = await policyProvider.GetEffectiveAudioPolicyAsync(userId, cancellationToken);

        var completed = isMusic
            ? progress >= audioPolicy.CompletedThresholdPercent / 100.0
              || position >= audioPolicy.CompletedMinDurationSeconds
            : progress >= videoPolicy.CompletedThresholdPercent / 100.0;

        var wasNewlyCompleted = false;
        Guid? episodeIdForEnqueue = null;

        if (completed)
        {
            if (!state.IsCompleted)
            {
                state.PlayCount++;
                state.IsCompleted = true;
                wasNewlyCompleted = true;
            }

            state.LastPlaybackPosition = 0;
            state.ProgressPercentage = 100;
            state.ExcludedFromContinueWatching = false;

            if (media is SerieEpisode episode)
                episodeIdForEnqueue = episode.Id;
        }
        else
        {
            if (!isMusic)
            {
                state.LastPlaybackPosition = position;
                state.ProgressPercentage = Math.Clamp(progress * 100, 0, 100);
            }

            if (!isMusic && ContinueWatchingEligibility.MeetsResumeThreshold(state, videoPolicy))
            {
                state.ExcludedFromContinueWatching = false;
                await exclusionService.ClearExclusionCascadeAsync(
                    userId, media, videoPolicy, timeNow, cancellationToken);
            }
        }

        return new UserMediaStateUpdateResult(
            state.ProgressPercentage,
            state.IsCompleted,
            wasNewlyCompleted,
            episodeIdForEnqueue);
    }
}
