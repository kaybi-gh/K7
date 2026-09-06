using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public interface IPlaybackBookmarkService
{
    Task UpsertItemBookmarkAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid mediaId,
        double position,
        double duration,
        DateTime timeNow,
        CancellationToken cancellationToken = default);

    Task RemoveItemBookmarkAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task OnEpisodeCompletedAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid episodeId,
        DateTime timeNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// After a library scan or new episode, recalc next-up for existing series bookmarks.
    /// Caught-up bookmarks stay dormant (null next) until a later episode is playable.
    /// When <paramref name="newlyPlayableEpisodeIds"/> is set, also recreates bookmarks for viewers
    /// whose next-up is one of those episodes. Dismissed next-up is not recreated because it is already playable.
    /// </summary>
    Task RefreshSeriesBookmarksForSerieAsync(
        Guid serieId,
        DateTime timeNow,
        IReadOnlyCollection<Guid>? newlyPlayableEpisodeIds = null,
        CancellationToken cancellationToken = default);

    Task<Guid?> ResolveNextPlayableEpisodeIdAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid lastCompletedEpisodeId,
        CancellationToken cancellationToken = default);

    Task DismissAsync(Guid mediaId, Guid userId, CancellationToken cancellationToken = default);

    Task DismissForSharedProfileAsync(
        Guid mediaId,
        Guid sharedProfileId,
        CancellationToken cancellationToken = default);

    bool MeetsItemResumeThreshold(ItemPlaybackBookmark bookmark, VideoPlaybackPolicySettingsDto policy);

    bool IsSeriesBookmarkEligible(
        SeriesPlaybackBookmark bookmark,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow,
        bool isNextPlayable);

    Task<Dictionary<Guid, ItemPlaybackBookmark>> GetItemBookmarksAsync(
        Guid? userId,
        Guid? sharedProfileId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default);

    Task ExpireStaleSeriesBookmarksAsync(
        Guid? userId,
        Guid? sharedProfileId,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills <see cref="SeriesPlaybackBookmark.NextEpisodeId"/> for bookmarks left empty by migration.
    /// </summary>
    Task BackfillMissingNextEpisodesAsync(
        Guid? userId,
        Guid? sharedProfileId,
        DateTime timeNow,
        CancellationToken cancellationToken = default);
}

public class PlaybackBookmarkService(
    IApplicationDbContext context,
    ILogger<PlaybackBookmarkService> logger) : IPlaybackBookmarkService
{
    public async Task UpsertItemBookmarkAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid mediaId,
        double position,
        double duration,
        DateTime timeNow,
        CancellationToken cancellationToken = default)
    {
        var bookmark = await FindItemBookmarkAsync(userId, sharedProfileId, mediaId, cancellationToken);
        if (bookmark is null)
        {
            bookmark = new ItemPlaybackBookmark
            {
                UserId = userId,
                SharedProfileId = sharedProfileId,
                MediaId = mediaId
            };
            context.PlaybackBookmarks.Add(bookmark);
        }

        bookmark.PositionSeconds = position;
        bookmark.DurationSeconds = duration > 0 ? duration : bookmark.DurationSeconds;
        bookmark.UpdatedAt = timeNow;
    }

    public async Task RemoveItemBookmarkAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var bookmark = await FindItemBookmarkAsync(userId, sharedProfileId, mediaId, cancellationToken);
        if (bookmark is not null)
            context.PlaybackBookmarks.Remove(bookmark);
    }

    public async Task OnEpisodeCompletedAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid episodeId,
        DateTime timeNow,
        CancellationToken cancellationToken = default)
    {
        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => e.Id == episodeId)
            .Select(e => new { e.SerieId })
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
            return;

        await RemoveItemBookmarkAsync(userId, sharedProfileId, episodeId, cancellationToken);

        var seriesBookmark = await FindSeriesBookmarkAsync(userId, sharedProfileId, episode.SerieId, cancellationToken);
        if (seriesBookmark is null)
        {
            seriesBookmark = new SeriesPlaybackBookmark
            {
                UserId = userId,
                SharedProfileId = sharedProfileId,
                SerieId = episode.SerieId,
                LastCompletedEpisodeId = episodeId,
                ActivityAt = timeNow,
                UpdatedAt = timeNow
            };
            context.PlaybackBookmarks.Add(seriesBookmark);
        }
        else
        {
            seriesBookmark.LastCompletedEpisodeId = episodeId;
            seriesBookmark.ActivityAt = timeNow;
            seriesBookmark.UpdatedAt = timeNow;
        }

        var nextEpisodeId = await ResolveNextPlayableEpisodeIdAsync(
            userId,
            sharedProfileId,
            episodeId,
            cancellationToken);

        ApplyNextEpisode(seriesBookmark, nextEpisodeId, timeNow);
    }

    public async Task RefreshSeriesBookmarksForSerieAsync(
        Guid serieId,
        DateTime timeNow,
        IReadOnlyCollection<Guid>? newlyPlayableEpisodeIds = null,
        CancellationToken cancellationToken = default)
    {
        var seriesBookmarks = await context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .Where(b => b.SerieId == serieId)
            .ToListAsync(cancellationToken);

        foreach (var bookmark in seriesBookmarks)
        {
            var nextEpisodeId = await ResolveNextPlayableEpisodeIdAsync(
                bookmark.UserId,
                bookmark.SharedProfileId,
                bookmark.LastCompletedEpisodeId,
                cancellationToken);

            ApplyNextEpisode(bookmark, nextEpisodeId, timeNow);
        }

        var recreated = 0;
        if (newlyPlayableEpisodeIds is { Count: > 0 })
        {
            recreated = await RecreateMissingSeriesBookmarksAsync(
                serieId,
                timeNow,
                newlyPlayableEpisodeIds,
                seriesBookmarks,
                cancellationToken);
        }

        logger.LogDebug(
            "Refreshed {Count} series playback bookmarks for serie {SerieId} ({Recreated} recreated)",
            seriesBookmarks.Count,
            serieId,
            recreated);
    }

    public async Task<Guid?> ResolveNextPlayableEpisodeIdAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid lastCompletedEpisodeId,
        CancellationToken cancellationToken = default)
    {
        var currentId = lastCompletedEpisodeId;
        for (var i = 0; i < 500; i++)
        {
            var nextId = await ResolveNextEpisodeIdAsync(currentId, cancellationToken);
            if (nextId is null)
                return null;

            if (!await IsEpisodePlayableAsync(nextId.Value, cancellationToken))
                return null;

            var isCompleted = await GetIsCompletedAsync(userId, sharedProfileId, nextId.Value, cancellationToken);
            if (isCompleted != true)
                return nextId;

            currentId = nextId.Value;
        }

        return null;
    }

    public async Task DismissAsync(Guid mediaId, Guid userId, CancellationToken cancellationToken = default)
    {
        var serieId = await ResolveSerieIdForDismissAsync(mediaId, cancellationToken);
        if (serieId is { } id)
        {
            await DismissSerieAsync(userId, sharedProfileId: null, id, cancellationToken);
            return;
        }

        var itemBookmark = await FindItemBookmarkAsync(userId, sharedProfileId: null, mediaId, cancellationToken);
        if (itemBookmark is not null)
            context.PlaybackBookmarks.Remove(itemBookmark);
    }

    public async Task DismissForSharedProfileAsync(
        Guid mediaId,
        Guid sharedProfileId,
        CancellationToken cancellationToken = default)
    {
        var serieId = await ResolveSerieIdForDismissAsync(mediaId, cancellationToken);
        if (serieId is { } id)
        {
            await DismissSerieAsync(userId: null, sharedProfileId, id, cancellationToken);
            return;
        }

        var itemBookmark = await FindItemBookmarkAsync(userId: null, sharedProfileId, mediaId, cancellationToken);
        if (itemBookmark is not null)
            context.PlaybackBookmarks.Remove(itemBookmark);
    }

    public bool MeetsItemResumeThreshold(ItemPlaybackBookmark bookmark, VideoPlaybackPolicySettingsDto policy)
    {
        if (policy.MinResumeDurationSeconds > 0
            && bookmark.DurationSeconds > 0
            && bookmark.DurationSeconds < policy.MinResumeDurationSeconds)
        {
            return false;
        }

        return bookmark.ProgressPercentage >= policy.MinResumePercent;
    }

    public bool IsSeriesBookmarkEligible(
        SeriesPlaybackBookmark bookmark,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow,
        bool isNextPlayable)
    {
        if (!isNextPlayable || bookmark.NextEpisodeId is null)
            return false;

        var cutoff = ContinueWatchingEligibility.GetWindowCutoff(policy, utcNow);
        return cutoff is null || bookmark.NextEpisodeAvailableAt >= cutoff;
    }

    public async Task<Dictionary<Guid, ItemPlaybackBookmark>> GetItemBookmarksAsync(
        Guid? userId,
        Guid? sharedProfileId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        if (mediaIds.Count == 0)
            return [];

        var query = context.PlaybackBookmarks
            .OfType<ItemPlaybackBookmark>()
            .AsNoTracking()
            .Where(b => mediaIds.Contains(b.MediaId));

        query = userId is { } uid
            ? query.Where(b => b.UserId == uid)
            : query.Where(b => b.SharedProfileId == sharedProfileId);

        return await query.ToDictionaryAsync(b => b.MediaId, cancellationToken);
    }

    public async Task ExpireStaleSeriesBookmarksAsync(
        Guid? userId,
        Guid? sharedProfileId,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var cutoff = ContinueWatchingEligibility.GetWindowCutoff(policy, utcNow);
        if (cutoff is null)
            return;

        var query = context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .Where(b => b.NextEpisodeId != null && b.NextEpisodeAvailableAt < cutoff);

        query = userId is { } uid
            ? query.Where(b => b.UserId == uid)
            : query.Where(b => b.SharedProfileId == sharedProfileId);

        var stale = await query.ToListAsync(cancellationToken);
        if (stale.Count == 0)
            return;

        var nextEpisodeIds = stale
            .Select(b => b.NextEpisodeId!.Value)
            .Distinct()
            .ToList();

        List<Guid> startedNextIds;
        if (userId.HasValue)
        {
            var ownerUserId = userId.Value;
            startedNextIds = await context.PlaybackBookmarks
                .OfType<ItemPlaybackBookmark>()
                .Where(b => nextEpisodeIds.Contains(b.MediaId) && b.UserId == ownerUserId)
                .Select(b => b.MediaId)
                .ToListAsync(cancellationToken);
        }
        else
        {
            startedNextIds = await context.PlaybackBookmarks
                .OfType<ItemPlaybackBookmark>()
                .Where(b => nextEpisodeIds.Contains(b.MediaId) && b.SharedProfileId == sharedProfileId)
                .Select(b => b.MediaId)
                .ToListAsync(cancellationToken);
        }
        var startedSet = startedNextIds.ToHashSet();

        foreach (var bookmark in stale)
        {
            if (bookmark.NextEpisodeId is { } nextId && startedSet.Contains(nextId))
                continue;

            context.PlaybackBookmarks.Remove(bookmark);
        }
    }

    public async Task BackfillMissingNextEpisodesAsync(
        Guid? userId,
        Guid? sharedProfileId,
        DateTime timeNow,
        CancellationToken cancellationToken = default)
    {
        var query = context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .Where(b => b.NextEpisodeId == null);

        query = userId is { } uid
            ? query.Where(b => b.UserId == uid)
            : query.Where(b => b.SharedProfileId == sharedProfileId);

        var incomplete = await query.ToListAsync(cancellationToken);
        if (incomplete.Count == 0)
            return;

        foreach (var bookmark in incomplete)
        {
            var nextEpisodeId = await ResolveNextPlayableEpisodeIdAsync(
                bookmark.UserId,
                bookmark.SharedProfileId,
                bookmark.LastCompletedEpisodeId,
                cancellationToken);

            if (nextEpisodeId is null)
                continue;

            bookmark.NextEpisodeId = nextEpisodeId;
            bookmark.NextEpisodeAvailableAt = timeNow;
            bookmark.UpdatedAt = timeNow;
        }
    }

    private async Task<Guid?> ResolveSerieIdForDismissAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        return media switch
        {
            SerieEpisode episode => episode.SerieId,
            SerieSeason season => season.SerieId,
            Serie => media.Id,
            _ => null
        };
    }

    private async Task<int> RecreateMissingSeriesBookmarksAsync(
        Guid serieId,
        DateTime timeNow,
        IReadOnlyCollection<Guid> newlyPlayableEpisodeIds,
        IReadOnlyCollection<SeriesPlaybackBookmark> existingBookmarks,
        CancellationToken cancellationToken)
    {
        var newlyPlayable = newlyPlayableEpisodeIds.ToHashSet();
        var existingUserIds = existingBookmarks
            .Where(b => b.UserId.HasValue)
            .Select(b => b.UserId!.Value)
            .ToHashSet();
        var existingProfileIds = existingBookmarks
            .Where(b => b.SharedProfileId.HasValue)
            .Select(b => b.SharedProfileId!.Value)
            .ToHashSet();

        var episodeRows = (await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SerieId == serieId)
            .Select(e => new { e.Id, SeasonNumber = e.Season.SeasonNumber, e.EpisodeNumber })
            .ToListAsync(cancellationToken))
            .Select(e => (e.Id, e.SeasonNumber, e.EpisodeNumber))
            .ToList();

        if (episodeRows.Count == 0)
            return 0;

        var episodeIds = episodeRows.Select(e => e.Id).ToList();
        var recreated = 0;

        var userCompletions = await context.UserMediaStates
            .Where(s => s.IsCompleted && episodeIds.Contains(s.MediaId))
            .Select(s => new { s.UserId, s.MediaId, s.LastInteractedAt })
            .ToListAsync(cancellationToken);

        foreach (var group in userCompletions.GroupBy(c => c.UserId))
        {
            if (!existingUserIds.Add(group.Key))
                continue;

            if (await TryRecreateSeriesBookmarkAsync(
                    userId: group.Key,
                    sharedProfileId: null,
                    serieId,
                    timeNow,
                    newlyPlayable,
                    episodeRows,
                    group.Select(c => (c.MediaId, c.LastInteractedAt)),
                    cancellationToken))
            {
                recreated++;
            }
        }

        var profileCompletions = await context.SharedProfileMediaStates
            .Where(s => s.IsCompleted && episodeIds.Contains(s.MediaId))
            .Select(s => new { s.SharedProfileId, s.MediaId, s.LastInteractedAt })
            .ToListAsync(cancellationToken);

        foreach (var group in profileCompletions.GroupBy(c => c.SharedProfileId))
        {
            if (!existingProfileIds.Add(group.Key))
                continue;

            if (await TryRecreateSeriesBookmarkAsync(
                    userId: null,
                    sharedProfileId: group.Key,
                    serieId,
                    timeNow,
                    newlyPlayable,
                    episodeRows,
                    group.Select(c => (c.MediaId, c.LastInteractedAt)),
                    cancellationToken))
            {
                recreated++;
            }
        }

        return recreated;
    }

    private async Task<bool> TryRecreateSeriesBookmarkAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid serieId,
        DateTime timeNow,
        HashSet<Guid> newlyPlayable,
        IReadOnlyCollection<(Guid Id, int SeasonNumber, int EpisodeNumber)> episodeRows,
        IEnumerable<(Guid MediaId, DateTime? LastInteractedAt)> completions,
        CancellationToken cancellationToken)
    {
        var last = completions
            .Join(
                episodeRows,
                c => c.MediaId,
                e => e.Id,
                (c, e) => new { c.MediaId, c.LastInteractedAt, e.SeasonNumber, e.EpisodeNumber })
            .OrderByDescending(x => x.SeasonNumber == 0 ? int.MinValue : x.SeasonNumber)
            .ThenByDescending(x => x.EpisodeNumber)
            .FirstOrDefault();

        if (last is null)
            return false;

        var nextEpisodeId = await ResolveNextPlayableEpisodeIdAsync(
            userId,
            sharedProfileId,
            last.MediaId,
            cancellationToken);

        if (nextEpisodeId is null || !newlyPlayable.Contains(nextEpisodeId.Value))
            return false;

        context.PlaybackBookmarks.Add(new SeriesPlaybackBookmark
        {
            UserId = userId,
            SharedProfileId = sharedProfileId,
            SerieId = serieId,
            LastCompletedEpisodeId = last.MediaId,
            NextEpisodeId = nextEpisodeId,
            ActivityAt = last.LastInteractedAt ?? timeNow,
            NextEpisodeAvailableAt = timeNow,
            UpdatedAt = timeNow
        });

        return true;
    }

    private static void ApplyNextEpisode(SeriesPlaybackBookmark bookmark, Guid? nextEpisodeId, DateTime timeNow)
    {
        if (bookmark.NextEpisodeId != nextEpisodeId)
        {
            bookmark.NextEpisodeId = nextEpisodeId;
            bookmark.NextEpisodeAvailableAt = nextEpisodeId is not null ? timeNow : default;
        }
        else if (nextEpisodeId is not null && bookmark.NextEpisodeAvailableAt == default)
        {
            bookmark.NextEpisodeAvailableAt = timeNow;
        }

        bookmark.UpdatedAt = timeNow;
    }

    private async Task DismissSerieAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid serieId,
        CancellationToken cancellationToken)
    {
        var seriesBookmark = await FindSeriesBookmarkAsync(userId, sharedProfileId, serieId, cancellationToken);
        if (seriesBookmark is not null)
            context.PlaybackBookmarks.Remove(seriesBookmark);

        var episodeIds = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SerieId == serieId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (episodeIds.Count == 0)
            return;

        List<ItemPlaybackBookmark> itemBookmarks;
        if (userId is { } uid)
        {
            itemBookmarks = await context.PlaybackBookmarks
                .OfType<ItemPlaybackBookmark>()
                .Where(b => episodeIds.Contains(b.MediaId) && b.UserId == uid)
                .ToListAsync(cancellationToken);
        }
        else
        {
            itemBookmarks = await context.PlaybackBookmarks
                .OfType<ItemPlaybackBookmark>()
                .Where(b => episodeIds.Contains(b.MediaId) && b.SharedProfileId == sharedProfileId)
                .ToListAsync(cancellationToken);
        }

        foreach (var bookmark in itemBookmarks)
            context.PlaybackBookmarks.Remove(bookmark);
    }

    private async Task<ItemPlaybackBookmark?> FindItemBookmarkAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var query = context.PlaybackBookmarks
            .OfType<ItemPlaybackBookmark>()
            .Where(b => b.MediaId == mediaId);

        query = userId is { } uid
            ? query.Where(b => b.UserId == uid)
            : query.Where(b => b.SharedProfileId == sharedProfileId);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<SeriesPlaybackBookmark?> FindSeriesBookmarkAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid serieId,
        CancellationToken cancellationToken)
    {
        var query = context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .Where(b => b.SerieId == serieId);

        query = userId is { } uid
            ? query.Where(b => b.UserId == uid)
            : query.Where(b => b.SharedProfileId == sharedProfileId);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool?> GetIsCompletedAsync(
        Guid? userId,
        Guid? sharedProfileId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        if (userId is { } uid)
        {
            return await context.UserMediaStates
                .Where(s => s.UserId == uid && s.MediaId == mediaId)
                .Select(s => (bool?)s.IsCompleted)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await context.SharedProfileMediaStates
            .Where(s => s.SharedProfileId == sharedProfileId && s.MediaId == mediaId)
            .Select(s => (bool?)s.IsCompleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsEpisodePlayableAsync(Guid episodeId, CancellationToken cancellationToken) =>
        await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.Id == episodeId)
            .AnyAsync(
                e => e.IndexedFiles.Any() || e.RemoteIndexedFiles.Any(),
                cancellationToken);

    private async Task<Guid?> ResolveNextEpisodeIdAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => e.Id == episodeId)
            .Select(e => new { e.SeasonId, e.SerieId, e.EpisodeNumber, SeasonNumber = e.Season.SeasonNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
            return null;

        var nextEpisode = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SeasonId == episode.SeasonId && e.EpisodeNumber > episode.EpisodeNumber)
            .OrderBy(e => e.EpisodeNumber)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextEpisode == default)
        {
            nextEpisode = await context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.SerieId == episode.SerieId && e.Season.SeasonNumber > episode.SeasonNumber)
                .OrderBy(e => e.Season.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return nextEpisode == default ? null : nextEpisode;
    }
}
