using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

internal sealed class HomeFeedContinueWatchingStrategy(
    IApplicationDbContext context,
    IPlaybackPolicySettingsProvider playbackPolicySettingsProvider,
    MediaAccessFilter mediaAccessFilter)
{
    public async Task<PaginatedList<HomeFeedItemDto>> HandleAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var videoPolicy = await playbackPolicySettingsProvider.GetEffectiveVideoPolicyAsync(userId.Value, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var query = context.Medias
            .AsNoTracking()
            .WhereEligibleForContinueWatching(userId.Value, videoPolicy, utcNow)
            .Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());

        query = HomeFeedQueryFilters.ApplyFamilyFilter(query, request.MediaTypes);
        query = HomeFeedQueryFilters.ApplyLibraryFilter(context, query, request.LibraryIds);
        query = await HomeFeedQueryFilters.ApplyUserExclusionsAsync(mediaAccessFilter, query, userId.Value, cancellationToken);

        var candidates = query.Select(x => new
        {
            x.Id,
            GroupId = x is SerieEpisode ? ((SerieEpisode)x).SerieId : x.Id,
            SeasonNumber = x is SerieEpisode ? ((SerieEpisode)x).Season.SeasonNumber : 0,
            EpisodeNumber = x is SerieEpisode ? ((SerieEpisode)x).EpisodeNumber : 0,
            State = x.UserMediaStates
                .Where(s => s.UserId == userId.Value)
                .Select(s => new
                {
                    s.IsCompleted,
                    s.LastPlaybackPosition,
                    s.ProgressPercentage,
                    s.LastInteractedAt
                })
                .FirstOrDefault()
        });

        var groupedCandidates = candidates
            .GroupBy(x => x.GroupId)
            .Select(group => new
            {
                GroupId = group.Key,
                LastInteractedAt = group.Max(x => x.State!.LastInteractedAt),
                MediaId = group
                    .OrderByDescending(x => x.State != null
                        && !x.State.IsCompleted
                        && (x.State.LastPlaybackPosition > 0
                            || (x.State.ProgressPercentage > 0 && x.State.ProgressPercentage < 100)))
                    .ThenByDescending(x => x.State!.LastInteractedAt)
                    .ThenBy(x => x.SeasonNumber == 0 ? int.MaxValue : x.SeasonNumber)
                    .ThenBy(x => x.EpisodeNumber)
                    .Select(x => x.Id)
                    .First()
            });

        var totalCount = await groupedCandidates.CountAsync(cancellationToken);
        var pageIds = await groupedCandidates
            .OrderByDescending(x => x.LastInteractedAt)
            .ThenByDescending(x => x.GroupId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => x.MediaId)
            .ToListAsync(cancellationToken);

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

        var pageItemsById = pageItems.ToDictionary(m => m.Id);
        var page = pageIds
            .Select(id => pageItemsById[id])
            .ToList();

        var pictureSizes = await HomeFeedQueryFilters.GetPictureSizesAsync(context, page, cancellationToken);
        var feedItems = page.Select(i => HomeFeedItemMapper.MapContinueWatchingItem(i, request.Detailed == true, pictureSizes)).ToList();
        return new PaginatedList<HomeFeedItemDto>(feedItems, totalCount, request.PageNumber, request.PageSize);
    }
}
