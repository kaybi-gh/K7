using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Services;

public static class ContinueWatchingEligibility
{
    /// <summary>
    /// Sub-second player ticks are not a real resume point. Treat them as still-zero
    /// so a next-episode placeholder is not ejected from Keep Watching.
    /// </summary>
    public const double PlaceholderNoisePositionSeconds = 1;

    /// <summary>
    /// Progress below 1% is noise from the first player ticks, not a started watch.
    /// </summary>
    public const double PlaceholderNoiseProgressPercent = 1;

    public static DateTime? GetWindowCutoff(VideoPlaybackPolicySettingsDto policy, DateTime utcNow) =>
        policy.ContinueWatchingMaxAgeDays > 0
            ? utcNow.AddDays(-policy.ContinueWatchingMaxAgeDays)
            : null;

    public static bool MeetsResumeThreshold(UserMediaState state, VideoPlaybackPolicySettingsDto policy) =>
        MeetsResumeThreshold(
            state.IsCompleted,
            state.LastInteractedAt,
            state.LastKnownDurationSeconds,
            state.ProgressPercentage,
            policy);

    public static bool MeetsResumeThreshold(SharedProfileMediaState state, VideoPlaybackPolicySettingsDto policy) =>
        MeetsResumeThreshold(
            state.IsCompleted,
            state.LastInteractedAt,
            state.LastKnownDurationSeconds,
            state.ProgressPercentage,
            policy);

    /// <summary>
    /// Next-episode Keep Watching placeholders: touched after finishing the previous episode,
    /// with no real resume point yet (progress and position stay at 0 so playback starts at the beginning).
    /// </summary>
    public static bool IsContinueWatchingPlaceholder(UserMediaState state) =>
        IsContinueWatchingPlaceholder(
            state.IsCompleted,
            state.LastInteractedAt,
            state.PlayCount,
            state.LastPlaybackPosition,
            state.ProgressPercentage);

    public static bool IsContinueWatchingPlaceholder(SharedProfileMediaState state) =>
        IsContinueWatchingPlaceholder(
            state.IsCompleted,
            state.LastInteractedAt,
            state.PlayCount,
            state.LastPlaybackPosition,
            state.ProgressPercentage);

    public static bool IsEligibleForContinueWatching(
        UserMediaState state,
        VideoPlaybackPolicySettingsDto policy,
        bool isSerieEpisode = false) =>
        MeetsResumeThreshold(state, policy)
        || IsContinueWatchingPlaceholder(state)
        || IsEarlySerieEpisodeWatch(state, policy, isSerieEpisode);

    public static bool IsEligibleForContinueWatching(
        SharedProfileMediaState state,
        VideoPlaybackPolicySettingsDto policy,
        bool isSerieEpisode = false) =>
        MeetsResumeThreshold(state, policy)
        || IsContinueWatchingPlaceholder(state)
        || IsEarlySerieEpisodeWatch(state, policy, isSerieEpisode);

    private static bool IsContinueWatchingPlaceholder(
        bool isCompleted,
        DateTime? lastInteractedAt,
        int playCount,
        double lastPlaybackPosition,
        double progressPercentage) =>
        !isCompleted
        && lastInteractedAt is not null
        && playCount == 0
        && lastPlaybackPosition < PlaceholderNoisePositionSeconds
        && progressPercentage < PlaceholderNoiseProgressPercent;

    private static bool IsEarlySerieEpisodeWatch(
        bool isCompleted,
        DateTime? lastInteractedAt,
        double progressPercentage,
        VideoPlaybackPolicySettingsDto policy,
        bool isSerieEpisode) =>
        isSerieEpisode
        && !isCompleted
        && lastInteractedAt is not null
        && progressPercentage < policy.MinResumePercent;

    private static bool IsEarlySerieEpisodeWatch(
        UserMediaState state,
        VideoPlaybackPolicySettingsDto policy,
        bool isSerieEpisode) =>
        IsEarlySerieEpisodeWatch(
            state.IsCompleted,
            state.LastInteractedAt,
            state.ProgressPercentage,
            policy,
            isSerieEpisode);

    private static bool IsEarlySerieEpisodeWatch(
        SharedProfileMediaState state,
        VideoPlaybackPolicySettingsDto policy,
        bool isSerieEpisode) =>
        IsEarlySerieEpisodeWatch(
            state.IsCompleted,
            state.LastInteractedAt,
            state.ProgressPercentage,
            policy,
            isSerieEpisode);

    private static bool MeetsResumeThreshold(
        bool isCompleted,
        DateTime? lastInteractedAt,
        double lastKnownDurationSeconds,
        double progressPercentage,
        VideoPlaybackPolicySettingsDto policy)
    {
        if (isCompleted)
            return false;

        if (lastInteractedAt is null)
            return false;

        // MinResumeDurationSeconds filters by total media runtime (player Duration), not time watched.
        if (policy.MinResumeDurationSeconds > 0
            && lastKnownDurationSeconds > 0
            && lastKnownDurationSeconds < policy.MinResumeDurationSeconds)
        {
            return false;
        }

        return progressPercentage >= policy.MinResumePercent;
    }

    public static bool IsWithinWindow(
        UserMediaState state,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow)
    {
        if (state.LastInteractedAt is null)
            return false;

        var cutoff = GetWindowCutoff(policy, utcNow);
        return cutoff is null || state.LastInteractedAt >= cutoff;
    }

    public static bool MeetsThreshold(
        UserMediaState state,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow)
    {
        if (state.ExcludedFromContinueWatching)
            return false;

        if (!IsEligibleForContinueWatching(state, policy))
            return false;

        return IsWithinWindow(state, policy, utcNow);
    }

    public static IQueryable<BaseMedia> WhereEligibleForContinueWatching(
        this IQueryable<BaseMedia> query,
        Guid userId,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow)
    {
        var minResumePercent = policy.MinResumePercent;
        var minResumeDurationSeconds = policy.MinResumeDurationSeconds;
        var cutoff = GetWindowCutoff(policy, utcNow);

        query = query
            .Where(x => !(x is MusicAlbum) && !(x is MusicTrack))
            .Where(x => x.UserMediaStates.Any(s =>
                s.UserId == userId
                && !s.IsCompleted
                && !s.ExcludedFromContinueWatching
                && s.LastInteractedAt != null
                && (minResumeDurationSeconds <= 0
                    || s.LastKnownDurationSeconds <= 0
                    || s.LastKnownDurationSeconds >= minResumeDurationSeconds)
                && (s.ProgressPercentage >= minResumePercent
                    || (s.PlayCount == 0
                        && s.LastPlaybackPosition < PlaceholderNoisePositionSeconds
                        && s.ProgressPercentage < PlaceholderNoiseProgressPercent)
                    || (x is SerieEpisode
                        && s.ProgressPercentage < minResumePercent))));

        if (cutoff is not null)
        {
            query = query.Where(x => x.UserMediaStates.Any(s =>
                s.UserId == userId && s.LastInteractedAt >= cutoff));
        }

        return query;
    }

    public static IQueryable<BaseMedia> WhereEligibleForSharedProfileContinueWatching(
        this IQueryable<BaseMedia> query,
        IApplicationDbContext context,
        Guid sharedProfileId,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow)
    {
        var minResumePercent = policy.MinResumePercent;
        var minResumeDurationSeconds = policy.MinResumeDurationSeconds;
        var cutoff = GetWindowCutoff(policy, utcNow);

        query = query
            .Where(x => !(x is MusicAlbum) && !(x is MusicTrack))
            .Where(x => context.SharedProfileMediaStates.Any(s =>
                s.SharedProfileId == sharedProfileId
                && s.MediaId == x.Id
                && !s.IsCompleted
                && !s.ExcludedFromContinueWatching
                && s.LastInteractedAt != null
                && (minResumeDurationSeconds <= 0
                    || s.LastKnownDurationSeconds <= 0
                    || s.LastKnownDurationSeconds >= minResumeDurationSeconds)
                && (s.ProgressPercentage >= minResumePercent
                    || (s.PlayCount == 0
                        && s.LastPlaybackPosition < PlaceholderNoisePositionSeconds
                        && s.ProgressPercentage < PlaceholderNoiseProgressPercent)
                    || (x is SerieEpisode
                        && s.ProgressPercentage < minResumePercent))));

        if (cutoff is not null)
        {
            query = query.Where(x => context.SharedProfileMediaStates.Any(s =>
                s.SharedProfileId == sharedProfileId
                && s.MediaId == x.Id
                && s.LastInteractedAt >= cutoff));
        }

        return query;
    }
}
