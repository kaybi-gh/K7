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
    Guid? CompletedEpisodeId);

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
    IPlaybackPolicySettingsProvider policyProvider,
    IPlaybackBookmarkService bookmarkService) : ISharedProfileMediaStateUpdater
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
                IsCompleted = false
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

        if (!isMusic)
        {
            if (result.IsCompleted)
            {
                await bookmarkService.RemoveItemBookmarkAsync(userId: null, sharedProfileId, mediaId, cancellationToken);
                if (result.CompletedEpisodeId is { } episodeId)
                    await bookmarkService.OnEpisodeCompletedAsync(userId: null, sharedProfileId, episodeId, timeNow, cancellationToken);
            }
            else
            {
                await bookmarkService.UpsertItemBookmarkAsync(
                    userId: null, sharedProfileId, mediaId, position, duration, timeNow, cancellationToken);
            }
        }

        return new SharedProfileMediaStateUpdateResult(
            result.ProgressPercentage,
            result.IsCompleted,
            result.WasNewlyCompleted,
            result.CompletedEpisodeId);
    }
}
