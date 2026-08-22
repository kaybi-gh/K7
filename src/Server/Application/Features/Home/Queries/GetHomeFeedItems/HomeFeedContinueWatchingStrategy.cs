using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

internal sealed class HomeFeedContinueWatchingStrategy(
    IApplicationDbContext context,
    IPlaybackPolicySettingsProvider playbackPolicySettingsProvider,
    IPlaybackBookmarkService bookmarkService,
    MediaAccessFilter mediaAccessFilter)
{
    public async Task<PaginatedList<HomeFeedItemDto>> HandleAsync(
        GetHomeFeedItemsQuery request,
        Guid? userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var videoPolicy = await playbackPolicySettingsProvider.GetEffectiveVideoPolicyAsync(
            userId.Value, sharedProfileId, cancellationToken);
        var utcNow = DateTime.UtcNow;
        var cutoff = ContinueWatchingEligibility.GetWindowCutoff(videoPolicy, utcNow);

        await bookmarkService.BackfillMissingNextEpisodesAsync(
            userId, sharedProfileId, utcNow, cancellationToken);
        await bookmarkService.ExpireStaleSeriesBookmarksAsync(
            userId, sharedProfileId, videoPolicy, utcNow, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var candidates = sharedProfileId is { } profileId
            ? await BuildSharedProfileCandidatesAsync(profileId, videoPolicy, cutoff, utcNow, cancellationToken)
            : await BuildPersonalCandidatesAsync(userId.Value, videoPolicy, cutoff, utcNow, cancellationToken);

        if (candidates.Count == 0)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var mediaIds = candidates.Select(c => c.MediaId).ToHashSet();
        var query = context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());

        query = HomeFeedQueryFilters.ApplyFamilyFilter(query, request.MediaTypes);
        query = HomeFeedQueryFilters.ApplyLibraryFilter(context, query, request.LibraryIds);
        query = await HomeFeedQueryFilters.ApplyUserExclusionsAsync(mediaAccessFilter, query, userId.Value, cancellationToken);

        var allowedIds = await query.Select(m => m.Id).ToListAsync(cancellationToken);
        var allowedSet = allowedIds.ToHashSet();
        var filtered = candidates
            .Where(c => allowedSet.Contains(c.MediaId))
            .ToList();

        var totalCount = filtered.Count;
        var pageIds = filtered
            .OrderByDescending(c => c.SortAt)
            .ThenByDescending(c => c.MediaId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => c.MediaId)
            .ToList();

        if (pageIds.Count == 0)
            return new PaginatedList<HomeFeedItemDto>([], totalCount, request.PageNumber, request.PageSize);

        var pageItems = await context.Medias
            .Where(m => pageIds.Contains(m.Id))
            .Include(x => x.Pictures)
            .Include(x => x.Ratings)
            .Include(x => x.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value))
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Pictures)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Ratings)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => ((SerieEpisode)x).Season).ThenInclude(s => s.Pictures)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var itemBookmarks = await bookmarkService.GetItemBookmarksAsync(
            sharedProfileId is null ? userId : null,
            sharedProfileId,
            pageIds,
            cancellationToken);

        var pageItemsById = pageItems.ToDictionary(m => m.Id);
        var page = pageIds.Select(id => pageItemsById[id]).ToList();

        var pictureSizes = await HomeFeedQueryFilters.GetPictureSizesAsync(context, page, cancellationToken);
        var feedItems = page.Select(i =>
        {
            var dto = HomeFeedItemMapper.MapContinueWatchingItem(i, request.Detailed == true, pictureSizes);
            itemBookmarks.TryGetValue(i.Id, out var bookmark);
            var state = i.UserMediaStates.FirstOrDefault();
            if (state is not null || bookmark is not null)
            {
                var overlay = state?.ToUserMediaStateDto(bookmark) ?? bookmark!.ToUserMediaStateDto();
                dto = dto with
                {
                    Progress = overlay.ProgressPercentage,
                    Watched = overlay.IsCompleted
                };
            }

            return dto;
        }).ToList();

        return new PaginatedList<HomeFeedItemDto>(feedItems, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<List<ContinueWatchingCandidate>> BuildPersonalCandidatesAsync(
        Guid userId,
        VideoPlaybackPolicySettingsDto policy,
        DateTime? cutoff,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var itemBookmarks = await context.PlaybackBookmarks
            .OfType<ItemPlaybackBookmark>()
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .Where(b => cutoff == null || b.UpdatedAt >= cutoff)
            .ToListAsync(cancellationToken);

        var seriesBookmarks = await context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .AsNoTracking()
            .Where(b => b.UserId == userId && b.NextEpisodeId != null)
            .ToListAsync(cancellationToken);

        return BuildCandidates(itemBookmarks, seriesBookmarks, policy, utcNow);
    }

    private async Task<List<ContinueWatchingCandidate>> BuildSharedProfileCandidatesAsync(
        Guid sharedProfileId,
        VideoPlaybackPolicySettingsDto policy,
        DateTime? cutoff,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var itemBookmarks = await context.PlaybackBookmarks
            .OfType<ItemPlaybackBookmark>()
            .AsNoTracking()
            .Where(b => b.SharedProfileId == sharedProfileId)
            .Where(b => cutoff == null || b.UpdatedAt >= cutoff)
            .ToListAsync(cancellationToken);

        var seriesBookmarks = await context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .AsNoTracking()
            .Where(b => b.SharedProfileId == sharedProfileId && b.NextEpisodeId != null)
            .ToListAsync(cancellationToken);

        return BuildCandidates(itemBookmarks, seriesBookmarks, policy, utcNow);
    }

    private List<ContinueWatchingCandidate> BuildCandidates(
        IReadOnlyList<ItemPlaybackBookmark> itemBookmarks,
        IReadOnlyList<SeriesPlaybackBookmark> seriesBookmarks,
        VideoPlaybackPolicySettingsDto policy,
        DateTime utcNow)
    {
        var candidates = new List<ContinueWatchingCandidate>();

        foreach (var bookmark in itemBookmarks)
        {
            if (!ContinueWatchingEligibility.IsItemBookmarkEligible(bookmark, policy, utcNow))
                continue;

            candidates.Add(new ContinueWatchingCandidate(bookmark.MediaId, bookmark.UpdatedAt, bookmark.MediaId));
        }

        foreach (var bookmark in seriesBookmarks)
        {
            if (!bookmarkService.IsSeriesBookmarkEligible(bookmark, policy, utcNow, isNextPlayable: true))
                continue;

            candidates.Add(new ContinueWatchingCandidate(
                bookmark.NextEpisodeId!.Value,
                bookmark.NextEpisodeAvailableAt,
                bookmark.SerieId));
        }

        return candidates
            .GroupBy(c => c.GroupId)
            .Select(g => g.OrderByDescending(c => c.SortAt).First())
            .ToList();
    }

    private sealed record ContinueWatchingCandidate(Guid MediaId, DateTime SortAt, Guid GroupId);
}

internal static class ItemPlaybackBookmarkMappings
{
    extension(ItemPlaybackBookmark bookmark)
    {
        public K7.Shared.Dtos.Entities.UserMediaStateDto ToUserMediaStateDto() => new()
        {
            LastPlaybackPosition = bookmark.PositionSeconds,
            ProgressPercentage = bookmark.ProgressPercentage,
            IsCompleted = false,
            PlayCount = 0,
            SkipCount = 0,
            LastInteractedAt = bookmark.UpdatedAt
        };
    }
}
