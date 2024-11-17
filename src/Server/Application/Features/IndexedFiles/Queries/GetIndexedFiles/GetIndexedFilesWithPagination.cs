using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFiles;

public record GetIndexedFilesWithPaginationQuery : IRequest<PaginatedList<IndexedFileDto>>
{
    public Guid? LibraryId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class GetIndexedFilesQueryHandler : IRequestHandler<GetIndexedFilesWithPaginationQuery, PaginatedList<IndexedFileDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetIndexedFilesQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedList<IndexedFileDto>> Handle(GetIndexedFilesWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.IndexedFiles;

        if (request.LibraryId.HasValue)
        {
            query.Where(x => x.LibraryId == request.LibraryId);
        }

        return await query.OrderBy(x => x.Path)
            .ProjectTo<IndexedFileDto>(_mapper.ConfigurationProvider)
            .PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
