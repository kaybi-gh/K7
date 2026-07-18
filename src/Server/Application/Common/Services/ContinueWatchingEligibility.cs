using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Services;

public static class ContinueWatchingEligibility
{
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

        if (!MeetsResumeThreshold(state, policy))
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
                && s.ProgressPercentage >= minResumePercent
                && (minResumeDurationSeconds <= 0
                    || s.LastKnownDurationSeconds <= 0
                    || s.LastKnownDurationSeconds >= minResumeDurationSeconds)));

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
                && s.ProgressPercentage >= minResumePercent
                && (minResumeDurationSeconds <= 0
                    || s.LastKnownDurationSeconds <= 0
                    || s.LastKnownDurationSeconds >= minResumeDurationSeconds)));

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
