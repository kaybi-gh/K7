using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Mappings;
using MediaServer.Application.Common.Models;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

public record GetMediasWithPaginationQuery : IRequest<PaginatedList<MediaDto>>
{
    public int? LibraryId { get; init; }
    public MediaType? MediaType { get; init; }
    // TODO - public bool? Seen { get; init; }
    // TODO - public MediaOrder? { get; init; } (order by added date, release date, plays, etc)
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetMediasQueryHandler : IRequestHandler<GetMediasWithPaginationQuery, PaginatedList<MediaDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetMediasQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedList<MediaDto>> Handle(GetMediasWithPaginationQuery request, CancellationToken cancellationToken)
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

        var medias = await query
            .PaginatedListAsync(request.PageNumber, request.PageSize);

        List<MediaDto> mediaDtos = [];
        foreach (var media in medias.Items)
        {
            switch (media.Type)
            {
                case MediaType.Movie:
                    var movieDto = _mapper.Map<MovieDto>(media);
                    mediaDtos.Add(movieDto);
                    break;
                default:
                    break;
            }
        }

        return new PaginatedList<MediaDto>(mediaDtos.AsReadOnly(), medias.TotalCount, request.PageNumber, request.PageSize);
    }
}
