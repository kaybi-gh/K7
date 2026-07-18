using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

internal sealed class HomeFeedTopLevelStrategy(
    IApplicationDbContext context,
    MediaAccessFilter mediaAccessFilter)
{
    public async Task<PaginatedList<HomeFeedItemDto>> HandleAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        // Only top-level entities: Movie, Serie, MusicAlbum
        var query = context.Medias
            .AsNoTracking()
            .Where(x => x is Movie || x is Serie || x is MusicAlbum);

        query = CatalogMediaAvailabilityHelper.WhereHasPlayableFiles(query);
        query = HomeFeedQueryFilters.ApplyFamilyFilter(query, request.MediaTypes);
        query = HomeFeedQueryFilters.ApplyLibraryFilter(context, query, request.LibraryIds);
        query = mediaAccessFilter.ApplyUnavailablePeerExclusion(query);

        if (userId.HasValue)
            query = await HomeFeedQueryFilters.ApplyUserExclusionsAsync(mediaAccessFilter, query, userId.Value, cancellationToken);

        var ordered = ApplyOrdering(request.OrderBy, query, userId);
        var totalCount = await ordered.CountAsync(cancellationToken);
        var pageIds = await ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (pageIds.Count == 0)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var items = await context.Medias
            .Where(m => pageIds.Contains(m.Id))
            .Include(x => x.Pictures)
            .Include(x => x.Ratings)
            .Include(x => x.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (userId.HasValue)
        {
            var userStates = await context.UserMediaStates
                .AsNoTracking()
                .Where(s => s.UserId == userId.Value && pageIds.Contains(s.MediaId))
                .ToDictionaryAsync(s => s.MediaId, cancellationToken);

            foreach (var item in items)
            {
                if (userStates.TryGetValue(item.Id, out var state))
                    item.UserMediaStates = [state];
            }
        }

        var pictureSizes = await HomeFeedQueryFilters.GetPictureSizesAsync(context, items, cancellationToken);
        var itemsById = items.ToDictionary(m => m.Id);
        var feedItems = pageIds
            .Select(id => itemsById[id])
            .Select(m => HomeFeedItemMapper.MapTopLevelItem(m, request.Detailed == true, pictureSizes))
            .ToList();

        return new PaginatedList<HomeFeedItemDto>(feedItems, totalCount, request.PageNumber, request.PageSize);
    }

    private static IOrderedQueryable<BaseMedia> ApplyOrdering(
        HashSet<MediaOrderingOption>? orderBy, IQueryable<BaseMedia> queryable, Guid? userId)
    {
        if (orderBy is not { Count: > 0 })
            return queryable.OrderByDescending(x => x.Id);

        IOrderedQueryable<BaseMedia>? ordered = null;

        foreach (var option in orderBy)
        {
            ordered = option switch
            {
                MediaOrderingOption.CreatedAsc => ordered?.ThenBy(x => x.Id) ?? queryable.OrderBy(x => x.Id),
                MediaOrderingOption.CreatedDesc => ordered?.ThenByDescending(x => x.Id) ?? queryable.OrderByDescending(x => x.Id),
                MediaOrderingOption.TitleAsc => ordered?.ThenBy(x => x.SortTitle ?? x.Title) ?? queryable.OrderBy(x => x.SortTitle ?? x.Title),
                MediaOrderingOption.TitleDesc => ordered?.ThenByDescending(x => x.SortTitle ?? x.Title) ?? queryable.OrderByDescending(x => x.SortTitle ?? x.Title),
                MediaOrderingOption.ReleaseDateAsc => ordered?.ThenBy(x => x.ReleaseDate) ?? queryable.OrderBy(x => x.ReleaseDate),
                MediaOrderingOption.ReleaseDateDesc => ordered?.ThenByDescending(x => x.ReleaseDate) ?? queryable.OrderByDescending(x => x.ReleaseDate),
                MediaOrderingOption.LocalRatingAsc => ordered?.ThenBy(x => x.Ratings
                    .OfType<K7.Server.Domain.Entities.Ratings.UserRating>()
                    .Where(r => !userId.HasValue || r.UserId == userId.Value)
                    .Select(r => (double?)r.Value).FirstOrDefault())
                    ?? queryable.OrderBy(x => x.Ratings
                    .OfType<K7.Server.Domain.Entities.Ratings.UserRating>()
                    .Where(r => !userId.HasValue || r.UserId == userId.Value)
                    .Select(r => (double?)r.Value).FirstOrDefault()),
                MediaOrderingOption.LocalRatingDesc => ordered?.ThenByDescending(x => x.Ratings
                    .OfType<K7.Server.Domain.Entities.Ratings.UserRating>()
                    .Where(r => !userId.HasValue || r.UserId == userId.Value)
                    .Select(r => (double?)r.Value).FirstOrDefault())
                    ?? queryable.OrderByDescending(x => x.Ratings
                    .OfType<K7.Server.Domain.Entities.Ratings.UserRating>()
                    .Where(r => !userId.HasValue || r.UserId == userId.Value)
                    .Select(r => (double?)r.Value).FirstOrDefault()),
                _ => ordered ?? queryable.OrderByDescending(x => x.Id)
            };
        }

        return ordered!;
    }
}
