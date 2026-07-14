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

        var isMusic = media.Type == MediaType.MusicTrack;
        var videoPolicy = await policyProvider.GetEffectiveVideoPolicyAsync(userId, cancellationToken);
        var audioPolicy = await policyProvider.GetEffectiveAudioPolicyAsync(userId, cancellationToken);

        var policy = isMusic
            ? new PlaybackProgressPolicy(true, audioPolicy.CompletedThresholdPercent, audioPolicy.CompletedMinDurationSeconds)
            : new PlaybackProgressPolicy(false, videoPolicy.CompletedThresholdPercent, 0);

        var result = state.RecordProgress(position, duration, policy, media, timeNow);

        if (!result.IsCompleted && !isMusic
            && ContinueWatchingEligibility.MeetsResumeThreshold(state, videoPolicy))
        {
            state.ExcludedFromContinueWatching = false;
            await exclusionService.ClearExclusionCascadeAsync(
                userId, media, videoPolicy, timeNow, cancellationToken);
        }

        return new UserMediaStateUpdateResult(
            result.ProgressPercentage,
            result.IsCompleted,
            result.WasNewlyCompleted,
            result.EpisodeIdForEnqueue);
    }
}
