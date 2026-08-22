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
    Guid? CompletedEpisodeId);

public class UserMediaState : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public bool IsCompleted { get; set; }
    public int PlayCount { get; set; }
    public int SkipCount { get; set; }
    public DateTime? LastInteractedAt { get; set; }

    public PlaybackProgressResult RecordProgress(
        double position,
        double duration,
        PlaybackProgressPolicy policy,
        BaseMedia media,
        DateTime timeNow)
    {
        LastInteractedAt = timeNow;

        var effectiveDuration = duration;
        if (duration > 0 && position > duration)
            effectiveDuration = position;

        var progress = effectiveDuration > 0 ? position / effectiveDuration : 0;

        var completed = policy.IsMusic
            ? progress >= policy.CompletedThresholdPercent / 100.0
              || position >= policy.CompletedMinDurationSeconds
            : progress >= policy.CompletedThresholdPercent / 100.0;

        var wasNewlyCompleted = false;
        Guid? completedEpisodeId = null;
        double progressPercentage;

        if (completed)
        {
            if (!IsCompleted)
            {
                PlayCount++;
                IsCompleted = true;
                wasNewlyCompleted = true;
            }

            progressPercentage = 100;

            if (media is SerieEpisode episode)
                completedEpisodeId = episode.Id;
        }
        else
        {
            if (!policy.IsMusic
                && IsCompleted
                && progress > 0
                && progress < policy.CompletedThresholdPercent / 100.0)
            {
                IsCompleted = false;
            }

            progressPercentage = Math.Clamp(progress * 100, 0, 100);
        }

        return new PlaybackProgressResult(
            progressPercentage,
            IsCompleted,
            wasNewlyCompleted,
            completedEpisodeId);
    }
}
