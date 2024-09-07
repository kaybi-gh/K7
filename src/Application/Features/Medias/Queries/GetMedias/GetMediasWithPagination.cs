using MediaServer.Application.Common.Converters;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Mappings;
using MediaServer.Application.Common.Models;
using MediaServer.Application.Common.Models.Dtos;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

public record GetMediasWithPaginationQuery : IRequest<PaginatedList<LiteMediaDto>>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? Ids { get; init; }
    // TODO - public bool? Seen { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public EnumHashSetQueryParam<MediaOrderingOption>? OrderBy { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetMediasQueryHandler : IRequestHandler<GetMediasWithPaginationQuery, PaginatedList<LiteMediaDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetMediasQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedList<LiteMediaDto>> Handle(GetMediasWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Medias
            .Include(x => x.Metadata)
                .ThenInclude(x => x!.ExternalIds)
            .Include(x => x.Metadata)
                .ThenInclude(x => x!.Pictures)
            .Include(x => x.Metadata)
                .ThenInclude(x => x!.Ratings)
            .Include(x => x.IndexedFiles)
            .AsQueryable();

        query = ApplyFilters(request, query);
        var orderedQuery = ApplyOrdering(request.OrderBy, query);
        var page = await orderedQuery.PaginatedListAsync(request.PageNumber, request.PageSize);

        List<LiteMediaDto> dtos = page.Items
            .Select(x => x.ConvertToLiteDto(_mapper))
            .ToList();

        return new PaginatedList<LiteMediaDto>(dtos.AsReadOnly(), page.TotalCount, request.PageNumber, request.PageSize);
    }

    private static IQueryable<BaseMedia> ApplyFilters(GetMediasWithPaginationQuery request, IQueryable<BaseMedia> query)
    {
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

        return query;
    }

    private static IOrderedQueryable<BaseMedia> ApplyOrdering(HashSet<MediaOrderingOption>? orderBy, IQueryable<BaseMedia> queryable)
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
                MediaOrderingOption.LocalRatingAsc => throw new NotImplementedException(),
                MediaOrderingOption.LocalRatingDesc => throw new NotImplementedException(),
                MediaOrderingOption.OriginalTitleAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Metadata!.OriginalTitle)
                    : orderedQueryable.ThenBy(x => x.Metadata!.OriginalTitle),
                MediaOrderingOption.OriginalTitleDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Metadata!.OriginalTitle)
                    : orderedQueryable.ThenByDescending(x => x.Metadata!.OriginalTitle),
                MediaOrderingOption.PlayCountAsc => throw new NotImplementedException(),
                MediaOrderingOption.PlayCountDesc => throw new NotImplementedException(),
                MediaOrderingOption.PopularityAsc => throw new NotImplementedException(),
                MediaOrderingOption.PopularityDesc => throw new NotImplementedException(),
                MediaOrderingOption.ReleaseDateAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Metadata!.ReleaseDate)
                    : orderedQueryable.ThenBy(x => x.Metadata!.ReleaseDate),
                MediaOrderingOption.ReleaseDateDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Metadata!.ReleaseDate)
                    : orderedQueryable.ThenByDescending(x => x.Metadata!.ReleaseDate),
                MediaOrderingOption.TitleAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Metadata!.Title)
                    : orderedQueryable.ThenBy(x => x.Metadata!.Title),
                MediaOrderingOption.TitleDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Metadata!.Title)
                    : orderedQueryable.ThenByDescending(x => x.Metadata!.Title),
                _ => throw new InvalidOperationException($"Unsupported media ordering option: {order}")
            };
        }
        return orderedQueryable!;
    }
}
