using K7.Server.Application.Common.Interfaces;
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

public class UserMediaStateUpdater(IApplicationDbContext context) : IUserMediaStateUpdater
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

        var progress = duration > 0 ? position / duration : 0;
        var isMusic = media.Type == MediaType.MusicTrack;
        var completed = isMusic
            ? progress >= 0.50 || position >= 240
            : progress >= 0.80;

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

            if (media is SerieEpisode episode)
                episodeIdForEnqueue = episode.Id;
        }
        else
        {
            if (state.IsCompleted && position < (duration * 0.1))
                state.IsCompleted = false;

            if (!isMusic)
            {
                state.LastPlaybackPosition = position;
                state.ProgressPercentage = Math.Clamp(progress * 100, 0, 100);
            }
        }

        return new UserMediaStateUpdateResult(
            state.ProgressPercentage,
            state.IsCompleted,
            wasNewlyCompleted,
            episodeIdForEnqueue);
    }
}
