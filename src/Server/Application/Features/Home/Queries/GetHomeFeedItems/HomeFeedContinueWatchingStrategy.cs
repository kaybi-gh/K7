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

        IQueryable<BaseMedia> query;
        if (sharedProfileId is { } profileId)
        {
            query = context.Medias
                .AsNoTracking()
                .WhereEligibleForSharedProfileContinueWatching(context, profileId, videoPolicy, utcNow)
                .Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());
        }
        else
        {
            query = context.Medias
                .AsNoTracking()
                .WhereEligibleForContinueWatching(userId.Value, videoPolicy, utcNow)
                .Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());
        }

        query = HomeFeedQueryFilters.ApplyFamilyFilter(query, request.MediaTypes);
        query = HomeFeedQueryFilters.ApplyLibraryFilter(context, query, request.LibraryIds);
        query = await HomeFeedQueryFilters.ApplyUserExclusionsAsync(mediaAccessFilter, query, userId.Value, cancellationToken);

        return sharedProfileId is { } sharedId
            ? await HandleSharedProfileAsync(request, sharedId, query, cancellationToken)
            : await HandlePersonalAsync(request, userId.Value, query, cancellationToken);
    }

    private async Task<PaginatedList<HomeFeedItemDto>> HandlePersonalAsync(
        GetHomeFeedItemsQuery request,
        Guid userId,
        IQueryable<BaseMedia> query,
        CancellationToken cancellationToken)
    {
        var groupedCandidates = query
            .Select(x => new
            {
                x.Id,
                GroupId = x is SerieEpisode ? ((SerieEpisode)x).SerieId : x.Id,
                SeasonNumber = x is SerieEpisode ? ((SerieEpisode)x).Season.SeasonNumber : 0,
                EpisodeNumber = x is SerieEpisode ? ((SerieEpisode)x).EpisodeNumber : 0,
                IsCompleted = x.UserMediaStates
                    .Where(s => s.UserId == userId)
                    .Select(s => (bool?)s.IsCompleted)
                    .FirstOrDefault() ?? false,
                LastPlaybackPosition = x.UserMediaStates
                    .Where(s => s.UserId == userId)
                    .Select(s => (double?)s.LastPlaybackPosition)
                    .FirstOrDefault() ?? 0,
                ProgressPercentage = x.UserMediaStates
                    .Where(s => s.UserId == userId)
                    .Select(s => (double?)s.ProgressPercentage)
                    .FirstOrDefault() ?? 0,
                LastInteractedAt = x.UserMediaStates
                    .Where(s => s.UserId == userId)
                    .Select(s => s.LastInteractedAt)
                    .FirstOrDefault()
            })
            .GroupBy(x => x.GroupId)
            .Select(g => new
            {
                GroupId = g.Key,
                LastInteractedAt = g.Max(x => x.LastInteractedAt),
                MediaId = g
                    .OrderByDescending(x => !x.IsCompleted
                        && (x.LastPlaybackPosition > 0
                            || (x.ProgressPercentage > 0 && x.ProgressPercentage < 100)))
                    .ThenByDescending(x => x.LastInteractedAt)
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
            .Include(x => x.UserMediaStates.Where(s => s.UserId == userId))
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Pictures)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Ratings)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => ((SerieEpisode)x).Season).ThenInclude(s => s.Pictures)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var pageItemsById = pageItems.ToDictionary(m => m.Id);
        var page = pageIds.Select(id => pageItemsById[id]).ToList();

        var pictureSizes = await HomeFeedQueryFilters.GetPictureSizesAsync(context, page, cancellationToken);
        var feedItems = page.Select(i => HomeFeedItemMapper.MapContinueWatchingItem(i, request.Detailed == true, pictureSizes)).ToList();
        return new PaginatedList<HomeFeedItemDto>(feedItems, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<PaginatedList<HomeFeedItemDto>> HandleSharedProfileAsync(
        GetHomeFeedItemsQuery request,
        Guid sharedProfileId,
        IQueryable<BaseMedia> query,
        CancellationToken cancellationToken)
    {
        var groupedCandidates = query
            .Select(x => new
            {
                x.Id,
                GroupId = x is SerieEpisode ? ((SerieEpisode)x).SerieId : x.Id,
                SeasonNumber = x is SerieEpisode ? ((SerieEpisode)x).Season.SeasonNumber : 0,
                EpisodeNumber = x is SerieEpisode ? ((SerieEpisode)x).EpisodeNumber : 0,
                IsCompleted = context.SharedProfileMediaStates
                    .Where(s => s.SharedProfileId == sharedProfileId && s.MediaId == x.Id)
                    .Select(s => (bool?)s.IsCompleted)
                    .FirstOrDefault() ?? false,
                LastPlaybackPosition = context.SharedProfileMediaStates
                    .Where(s => s.SharedProfileId == sharedProfileId && s.MediaId == x.Id)
                    .Select(s => (double?)s.LastPlaybackPosition)
                    .FirstOrDefault() ?? 0,
                ProgressPercentage = context.SharedProfileMediaStates
                    .Where(s => s.SharedProfileId == sharedProfileId && s.MediaId == x.Id)
                    .Select(s => (double?)s.ProgressPercentage)
                    .FirstOrDefault() ?? 0,
                LastInteractedAt = context.SharedProfileMediaStates
                    .Where(s => s.SharedProfileId == sharedProfileId && s.MediaId == x.Id)
                    .Select(s => s.LastInteractedAt)
                    .FirstOrDefault()
            })
            .GroupBy(x => x.GroupId)
            .Select(g => new
            {
                GroupId = g.Key,
                LastInteractedAt = g.Max(x => x.LastInteractedAt),
                MediaId = g
                    .OrderByDescending(x => !x.IsCompleted
                        && (x.LastPlaybackPosition > 0
                            || (x.ProgressPercentage > 0 && x.ProgressPercentage < 100)))
                    .ThenByDescending(x => x.LastInteractedAt)
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
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Pictures)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Ratings)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => ((SerieEpisode)x).Season).ThenInclude(s => s.Pictures)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var sharedStates = await context.SharedProfileMediaStates
            .AsNoTracking()
            .Where(s => s.SharedProfileId == sharedProfileId && pageIds.Contains(s.MediaId))
            .Select(s => new { s.MediaId, s.ProgressPercentage, s.IsCompleted })
            .ToDictionaryAsync(s => s.MediaId, cancellationToken);

        var pageItemsById = pageItems.ToDictionary(m => m.Id);
        var page = pageIds.Select(id => pageItemsById[id]).ToList();

        var pictureSizes = await HomeFeedQueryFilters.GetPictureSizesAsync(context, page, cancellationToken);
        var feedItems = page
            .Select(i =>
            {
                var dto = HomeFeedItemMapper.MapContinueWatchingItem(i, request.Detailed == true, pictureSizes);
                if (sharedStates.TryGetValue(i.Id, out var state))
                {
                    dto = dto with
                    {
                        Progress = state.ProgressPercentage,
                        Watched = state.IsCompleted
                    };
                }
                return dto;
            })
            .ToList();

        return new PaginatedList<HomeFeedItemDto>(feedItems, totalCount, request.PageNumber, request.PageSize);
    }
}
