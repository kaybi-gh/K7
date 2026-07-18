using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Services;

public sealed record SharedProfileMediaStateUpdateResult(
    double ProgressPercentage,
    bool IsCompleted,
    bool WasNewlyCompleted,
    Guid? EpisodeIdForEnqueue);

public interface ISharedProfileMediaStateUpdater
{
    Task<SharedProfileMediaStateUpdateResult> ApplyAsync(
        Guid sharedProfileId,
        BaseMedia media,
        Guid mediaId,
        double position,
        double duration,
        DateTime timeNow,
        CancellationToken cancellationToken = default);
}

public class SharedProfileMediaStateUpdater(
    IApplicationDbContext context,
    IPlaybackPolicySettingsProvider policyProvider) : ISharedProfileMediaStateUpdater
{
    public async Task<SharedProfileMediaStateUpdateResult> ApplyAsync(
        Guid sharedProfileId,
        BaseMedia media,
        Guid mediaId,
        double position,
        double duration,
        DateTime timeNow,
        CancellationToken cancellationToken = default)
    {
        var state = await context.SharedProfileMediaStates
            .FirstOrDefaultAsync(s => s.SharedProfileId == sharedProfileId && s.MediaId == mediaId, cancellationToken);

        if (state is null)
        {
            state = new SharedProfileMediaState
            {
                SharedProfileId = sharedProfileId,
                MediaId = mediaId,
                PlayCount = 0,
                IsCompleted = false,
                LastPlaybackPosition = 0
            };
            context.SharedProfileMediaStates.Add(state);
        }

        var isMusic = media.Type == MediaType.MusicTrack;
        var videoPolicy = await policyProvider.GetEffectiveVideoPolicyAsync(
            userId: null, sharedProfileId, cancellationToken);
        var audioPolicy = await policyProvider.GetEffectiveAudioPolicyAsync(
            userId: null, sharedProfileId, cancellationToken);

        var policy = isMusic
            ? new PlaybackProgressPolicy(true, audioPolicy.CompletedThresholdPercent, audioPolicy.CompletedMinDurationSeconds)
            : new PlaybackProgressPolicy(false, videoPolicy.CompletedThresholdPercent, 0);

        var result = state.RecordProgress(position, duration, policy, media, timeNow);

        if (!result.IsCompleted && !isMusic
            && ContinueWatchingEligibility.MeetsResumeThreshold(state, videoPolicy))
        {
            state.ExcludedFromContinueWatching = false;
        }

        return new SharedProfileMediaStateUpdateResult(
            result.ProgressPercentage,
            result.IsCompleted,
            result.WasNewlyCompleted,
            result.EpisodeIdForEnqueue);
    }
}
