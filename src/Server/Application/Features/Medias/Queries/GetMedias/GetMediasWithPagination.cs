using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Medias.Queries.GetMedias;

public record GetMediasWithPaginationQuery : IRequest<PaginatedList<BaseMedia>>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? Ids { get; init; }
    // TODO - public bool? Seen { get; init; }
    public bool? ContinueWatching { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public EnumHashSetQueryParam<MediaOrderingOption>? OrderBy { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetMediasQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMediasWithPaginationQuery, PaginatedList<BaseMedia>>
{
    public async Task<PaginatedList<BaseMedia>> Handle(GetMediasWithPaginationQuery request, CancellationToken cancellationToken)
    {
        Guid? userId = currentUser.Id;

        var query = context.Medias
            .Include(x => x.ExternalIds)
            .Include(x => x.Pictures)
            .Include(x => x.Ratings)
            .Include(x => x.IndexedFiles)
            .AsNoTracking()
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        query = ApplyFilters(request, query, userId);
        var orderedQuery = ApplyOrdering(request.OrderBy, query, userId);
        return await orderedQuery.PaginatedListAsync(request.PageNumber, request.PageSize);
    }

    private static IQueryable<BaseMedia> ApplyFilters(GetMediasWithPaginationQuery request, IQueryable<BaseMedia> query, Guid? userId)
    {
        query = query.Where(x => x.IndexedFiles.Any());

        if (request.LibraryIds?.Length > 0)
        {
            query = query.Where(x => x.IndexedFiles != null && x.IndexedFiles.Any(x => request.LibraryIds.Contains(x.LibraryId)));
        }

        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
        }

        if (request.MediaTypes?.Count > 0)
        {
            query = query.Where(x => request.MediaTypes.Contains(x.Type));
        }

        if (request.ContinueWatching == true && userId.HasValue)
        {
            query = query.Where(x => x.UserMediaStates.Any(s =>
                s.UserId == userId.Value
                && !s.IsCompleted
                && s.LastPlaybackPosition > 0));
        }

        return query;
    }

    private static IOrderedQueryable<BaseMedia> ApplyOrdering(HashSet<MediaOrderingOption>? orderBy, IQueryable<BaseMedia> queryable, Guid? userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nameof(orderBy));
        IOrderedQueryable<BaseMedia>? orderedQueryable = null;

        if (orderBy == null || orderBy.Count == 0)
        {
            return queryable.OrderByDescending(x => x.Id);
        }

        foreach (var order in orderBy)
        {
            orderedQueryable = order switch
            {
                MediaOrderingOption.CreatedAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Id)
                    : orderedQueryable.ThenBy(x => x.Id),
                MediaOrderingOption.CreatedDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Id)
                    : orderedQueryable.ThenByDescending(x => x.Id),
                MediaOrderingOption.LastInteractedAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.LastInteractedAt)
                        .FirstOrDefault())
                    : orderedQueryable.ThenBy(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.LastInteractedAt)
                        .FirstOrDefault()),
                MediaOrderingOption.LastInteractedDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.LastInteractedAt)
                        .FirstOrDefault())
                    : orderedQueryable.ThenByDescending(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.LastInteractedAt)
                        .FirstOrDefault()),
                MediaOrderingOption.LocalRatingAsc => throw new NotImplementedException(),
                MediaOrderingOption.LocalRatingDesc => throw new NotImplementedException(),
                MediaOrderingOption.OriginalTitleAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.OriginalTitle)
                    : orderedQueryable.ThenBy(x => x.OriginalTitle),
                MediaOrderingOption.OriginalTitleDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.OriginalTitle)
                    : orderedQueryable.ThenByDescending(x => x.OriginalTitle),
                MediaOrderingOption.PlayCountAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.PlayCount)
                        .FirstOrDefault())
                    : orderedQueryable.ThenBy(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.PlayCount)
                        .FirstOrDefault()),
                MediaOrderingOption.PlayCountDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.PlayCount)
                        .FirstOrDefault())
                    : orderedQueryable.ThenByDescending(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.PlayCount)
                        .FirstOrDefault()),
                MediaOrderingOption.PopularityAsc => throw new NotImplementedException(),
                MediaOrderingOption.PopularityDesc => throw new NotImplementedException(),
                MediaOrderingOption.ReleaseDateAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.ReleaseDate)
                    : orderedQueryable.ThenBy(x => x.ReleaseDate),
                MediaOrderingOption.ReleaseDateDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.ReleaseDate)
                    : orderedQueryable.ThenByDescending(x => x.ReleaseDate),
                MediaOrderingOption.TitleAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Title)
                    : orderedQueryable.ThenBy(x => x.Title),
                MediaOrderingOption.TitleDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Title)
                    : orderedQueryable.ThenByDescending(x => x.Title),
                _ => throw new InvalidOperationException($"Unsupported media ordering option: {order}")
            };
        }
        return orderedQueryable!;
    }
}
