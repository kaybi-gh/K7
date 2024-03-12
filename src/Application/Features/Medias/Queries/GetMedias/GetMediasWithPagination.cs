using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Mappings;
using MediaServer.Application.Common.Models;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

public record GetMediasWithPaginationQuery : IRequest<PaginatedList<BaseMedia>>
{
    public int? LibraryId { get; init; }
    public MediaType? MediaType { get; init; }
    // TODO - public bool? Seen { get; init; }
    // TODO - public MediaOrder? { get; init; } (order by added date, release date, plays, etc)
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetMediasQueryHandler : IRequestHandler<GetMediasWithPaginationQuery, PaginatedList<BaseMedia>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetMediasQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedList<BaseMedia>> Handle(GetMediasWithPaginationQuery request, CancellationToken cancellationToken)
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

        if (request.LibraryId.HasValue)
        {
            query = query.Where(x => x.IndexedFiles != null && x.IndexedFiles.Any(x => x.LibraryId == request.LibraryId));
        }

        if (request.MediaType != null)
        {
            query = query.Where(x => x.Type == request.MediaType);
        }

        /*var test = await query
            .OrderByDescending(x => x.Created)
            .ToListAsync();
        var test2 = new List<MediaDto>();
        var mapper = new Mapper(_mapper.ConfigurationProvider);

        foreach (var item in test)
        {
            var mapped = mapper.Map(item, item.GetType(), typeof(MediaDto));
            test2.Add((MediaDto)mapped);
        }

        var movieDtos = test.Select(movie => _mapper.Map<MovieDto>(movie)).ToList();

        var test53 = await query
            .OrderByDescending(x => x.Created)
            .ProjectTo<MovieDto>(_mapper.ConfigurationProvider)
            .PaginatedListAsync(request.PageNumber, request.PageSize);*/

        return await query
            .OrderByDescending(x => x.Created)
            //.ProjectTo<MediaDto>(_mapper.ConfigurationProvider)
            .PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
