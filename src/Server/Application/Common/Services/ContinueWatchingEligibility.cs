using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Services;

public static class ContinueWatchingEligibility
{
    /// <summary>Ignore sub-second ticks when treating an early serie episode as in-progress.</summary>
    public const double EarlyResumeMinPositionSeconds = 1;

    /// <summary>Progress below this percent is still "early" for serie episodes under MinResumePercent.</summary>
    public const double EarlyResumeMaxProgressPercent = 1;

    public static DateTime? GetWindowCutoff(VideoPlaybackPolicySettingsDto policy, DateTime utcNow) =>
        policy.ContinueWatchingMaxAgeDays > 0
            ? utcNow.AddDays(-policy.ContinueWatchingMaxAgeDays)
            : null;

    public static bool MeetsItemResumeThreshold(
        ItemPlaybackBookmark bookmark,
        VideoPlaybackPolicySettingsDto policy)
    {
        if (policy.MinResumeDurationSeconds > 0
            && bookmark.DurationSeconds > 0
            && bookmark.DurationSeconds < policy.MinResumeDurationSeconds)
        {
            return false;
        }

        return bookmark.ProgressPercentage >= policy.MinResumePercent;
    }

    public static bool IsEarlySerieEpisodeBookmark(
        ItemPlaybackBookmark bookmark,
        VideoPlaybackPolicySettingsDto policy) =>
        bookmark.ProgressPercentage < policy.MinResumePercent
        && bookmark.PositionSeconds >= EarlyResumeMinPositionSeconds;

    public static bool IsItemBookmarkEligible(
        ItemPlaybackBookmark bookmark,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow,
        bool isSerieEpisode = false)
    {
        var cutoff = GetWindowCutoff(policy, utcNow);
        if (cutoff is not null && bookmark.UpdatedAt < cutoff)
            return false;

        return MeetsItemResumeThreshold(bookmark, policy)
            || (isSerieEpisode && IsEarlySerieEpisodeBookmark(bookmark, policy));
    }

    public static bool IsSeriesBookmarkEligible(
        SeriesPlaybackBookmark bookmark,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow,
        bool isNextPlayable)
    {
        if (!isNextPlayable || bookmark.NextEpisodeId is null)
            return false;

        var cutoff = GetWindowCutoff(policy, utcNow);
        return cutoff is null || bookmark.NextEpisodeAvailableAt >= cutoff;
    }

    public static IQueryable<BaseMedia> WhereEligibleForContinueWatching(
        this IQueryable<BaseMedia> query,
        IApplicationDbContext context,
        Guid userId,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow)
    {
        var minResumePercent = policy.MinResumePercent;
        var minResumeDurationSeconds = policy.MinResumeDurationSeconds;
        var cutoff = GetWindowCutoff(policy, utcNow);

        query = query
            .Where(x => !(x is MusicAlbum) && !(x is MusicTrack))
            .Where(x =>
                context.PlaybackBookmarks.OfType<ItemPlaybackBookmark>().Any(b =>
                    b.UserId == userId
                    && b.MediaId == x.Id
                    && (cutoff == null || b.UpdatedAt >= cutoff)
                    && (minResumeDurationSeconds <= 0
                        || b.DurationSeconds <= 0
                        || b.DurationSeconds >= minResumeDurationSeconds)
                    && (b.PositionSeconds / (b.DurationSeconds > 0 ? b.DurationSeconds : 1) * 100 >= minResumePercent
                        || (x is SerieEpisode
                            && b.PositionSeconds >= EarlyResumeMinPositionSeconds
                            && b.PositionSeconds / (b.DurationSeconds > 0 ? b.DurationSeconds : 1) * 100 < minResumePercent)))
                || context.PlaybackBookmarks.OfType<SeriesPlaybackBookmark>().Any(b =>
                    b.UserId == userId
                    && b.NextEpisodeId == x.Id
                    && (cutoff == null || b.NextEpisodeAvailableAt >= cutoff)));

        return query.Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());
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
            .Where(x =>
                context.PlaybackBookmarks.OfType<ItemPlaybackBookmark>().Any(b =>
                    b.SharedProfileId == sharedProfileId
                    && b.MediaId == x.Id
                    && (cutoff == null || b.UpdatedAt >= cutoff)
                    && (minResumeDurationSeconds <= 0
                        || b.DurationSeconds <= 0
                        || b.DurationSeconds >= minResumeDurationSeconds)
                    && (b.PositionSeconds / (b.DurationSeconds > 0 ? b.DurationSeconds : 1) * 100 >= minResumePercent
                        || (x is SerieEpisode
                            && b.PositionSeconds >= EarlyResumeMinPositionSeconds
                            && b.PositionSeconds / (b.DurationSeconds > 0 ? b.DurationSeconds : 1) * 100 < minResumePercent)))
                || context.PlaybackBookmarks.OfType<SeriesPlaybackBookmark>().Any(b =>
                    b.SharedProfileId == sharedProfileId
                    && b.NextEpisodeId == x.Id
                    && (cutoff == null || b.NextEpisodeAvailableAt >= cutoff)));

        return query.Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());
    }
}
