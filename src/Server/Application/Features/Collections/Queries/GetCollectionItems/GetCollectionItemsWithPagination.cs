using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Features.Collections.Queries.GetCollectionItems;

public record GetCollectionItemsWithPaginationQuery : IRequest<PaginatedList<CollectionItem>>
{
    public required Guid CollectionId { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.ItemsPageSize;
    public bool IncludeUnavailable { get; init; }
}

public class GetCollectionItemsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetCollectionItemsWithPaginationQuery, PaginatedList<CollectionItem>>
{
    public async Task<PaginatedList<CollectionItem>> Handle(
        GetCollectionItemsWithPaginationQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var collection = await context.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId && (c.IsPublic || c.UserId == userId), cancellationToken);

        Guard.Against.NotFound(request.CollectionId, collection);

        var query = context.CollectionItems
            .Include(i => i.Media)
                .ThenInclude(m => m.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(i => i.Media)
                .ThenInclude(m => m.IndexedFiles)
            .Include(i => i.Media)
                .ThenInclude(m => m.PersonRoles)
                    .ThenInclude(r => r.Person)
            .Include(i => i.Media)
                .ThenInclude(m => m.Ratings)
            .Include(i => (i.Media as MusicAlbum)!.Tracks)
            .Include(i => (i.Media as SerieEpisode)!.Season)
            .Include(i => (i.Media as SerieSeason)!.Serie)
                .ThenInclude(s => s!.Pictures)
                    .ThenInclude(p => p.Variants)
            .Where(i => i.CollectionId == request.CollectionId)
            .OrderBy(i => i.Order)
            .AsSplitQuery()
            .AsNoTracking();

        if (userId is { } currentUserId)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == currentUserId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            var excludedMediaIds = context.UserMediaExclusions
                .Where(e => e.UserId == currentUserId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.MediaId);

            query = query.Where(i => !excludedMediaIds.Contains(i.MediaId));

            // Series, seasons, albums and artists have no files of their own.
            // Availability is the episode or track linkage, same table the library browse uses.
            if (request.IncludeUnavailable)
            {
                query = query.Where(i =>
                    !context.MediaLibraryAvailabilities.Any(a => a.MediaId == i.MediaId)
                    || context.MediaLibraryAvailabilities.Any(a =>
                        a.MediaId == i.MediaId && !excludedLibraryIds.Contains(a.LibraryId)));
            }
            else
            {
                query = query.Where(i => context.MediaLibraryAvailabilities.Any(a =>
                    a.MediaId == i.MediaId && !excludedLibraryIds.Contains(a.LibraryId)));
            }

            var restrictionProfile = await context.ContentRestrictionProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == currentUserId), cancellationToken);

            if (restrictionProfile is not null)
            {
                var restrictedMediaIds = ContentRestrictionEvaluator.GetRestricted(
                    context.Medias.AsNoTracking(), restrictionProfile)
                    .Select(m => m.Id);

                query = query.Where(i => !restrictedMediaIds.Contains(i.MediaId));
            }
        }

        var page = await query.PaginatedListAsync(request.PageNumber, request.PageSize);
        MediaRatingScope.ScopeToUser(page.Items.Select(item => item.Media), userId);
        if (userId is { } id)
        {
            await UserWatchStateAttacher.AttachAsync(
                context,
                page.Items.Select(item => item.Media),
                id,
                cancellationToken);
        }

        return page;
    }
}
