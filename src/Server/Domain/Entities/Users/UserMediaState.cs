using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Users;

public sealed record PlaybackProgressPolicy(
    bool IsMusic,
    double CompletedThresholdPercent,
    double CompletedMinDurationSeconds);

public sealed record PlaybackProgressResult(
    double ProgressPercentage,
    bool IsCompleted,
    bool WasNewlyCompleted,
    Guid? EpisodeIdForEnqueue);

public class UserMediaState : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public double LastPlaybackPosition { get; set; }
    public double ProgressPercentage { get; set; }
    public bool IsCompleted { get; set; }
    public int PlayCount { get; set; }
    public int SkipCount { get; set; }
    public DateTime? LastInteractedAt { get; set; }
    public double LastKnownDurationSeconds { get; set; }
    public bool ExcludedFromContinueWatching { get; set; }

    public PlaybackProgressResult RecordProgress(
        double position,
        double duration,
        PlaybackProgressPolicy policy,
        BaseMedia media,
        DateTime timeNow)
    {
        LastInteractedAt = timeNow;
        // Prefer a duration that can contain the resume position. A short/wrong player duration
        // would otherwise mark the title completed and eject it from Keep Watching.
        var effectiveDuration = duration;
        if (duration > 0 && position > duration)
            effectiveDuration = position;

        LastKnownDurationSeconds = effectiveDuration > 0 ? effectiveDuration : duration;

        var progress = effectiveDuration > 0 ? position / effectiveDuration : 0;
        var completed = policy.IsMusic
            ? progress >= policy.CompletedThresholdPercent / 100.0
              || position >= policy.CompletedMinDurationSeconds
            : progress >= policy.CompletedThresholdPercent / 100.0;

        var wasNewlyCompleted = false;
        Guid? episodeIdForEnqueue = null;

        if (completed)
        {
            if (!IsCompleted)
            {
                PlayCount++;
                IsCompleted = true;
                wasNewlyCompleted = true;
            }

            LastPlaybackPosition = 0;
            ProgressPercentage = 100;
            ExcludedFromContinueWatching = false;

            if (media is SerieEpisode episode)
                episodeIdForEnqueue = episode.Id;
        }
        else
        {
            if (!policy.IsMusic)
            {
                // Re-open Keep Watching after a false completion (e.g. bogus short duration).
                if (IsCompleted && progress > 0 && progress < policy.CompletedThresholdPercent / 100.0)
                    IsCompleted = false;

                LastPlaybackPosition = position;
                ProgressPercentage = Math.Clamp(progress * 100, 0, 100);
            }
        }

        return new PlaybackProgressResult(
            ProgressPercentage,
            IsCompleted,
            wasNewlyCompleted,
            episodeIdForEnqueue);
    }
}
