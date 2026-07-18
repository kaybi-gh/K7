using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

internal sealed class HomeFeedRecommendedStrategy(
    IApplicationDbContext context,
    IHomeRecommendationService homeRecommendationService)
{
    public async Task<PaginatedList<HomeFeedItemDto>> HandleAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var recommendedIds = await homeRecommendationService.GetRecommendedMediaIdsAsync(
            userId.Value,
            request.LibraryIds,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        if (recommendedIds.Count == 0)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var items = await context.Medias
            .AsNoTracking()
            .Where(m => recommendedIds.Contains(m.Id))
            .Include(x => x.Pictures)
            .Include(x => x.Ratings)
            .Include(x => x.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var itemsById = items.ToDictionary(i => i.Id);
        var orderedItems = recommendedIds
            .Select(id => itemsById.GetValueOrDefault(id))
            .Where(i => i is not null)
            .Cast<BaseMedia>()
            .ToList();

        var pictureSizes = await HomeFeedQueryFilters.GetPictureSizesAsync(context, orderedItems, cancellationToken);
        var feedItems = orderedItems.Select(i => HomeFeedItemMapper.MapTopLevelItem(i, request.Detailed == true, pictureSizes)).ToList();
        return new PaginatedList<HomeFeedItemDto>(feedItems, feedItems.Count, request.PageNumber, request.PageSize);
    }
}
