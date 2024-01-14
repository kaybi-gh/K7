using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Mappings;
using MediaServer.Application.Common.Models;

namespace MediaServer.Application.Features.Libraries.Queries.GetLibrariesIndexedFiles;

public record GetLibraryIndexedFilesWithPaginationQuery : IRequest<PaginatedList<IndexedFileDto>>
{
    public required int LibraryId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class GetLibraryIndexedFilesQueryHandler : IRequestHandler<GetLibraryIndexedFilesWithPaginationQuery, PaginatedList<IndexedFileDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetLibraryIndexedFilesQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedList<IndexedFileDto>> Handle(GetLibraryIndexedFilesWithPaginationQuery request, CancellationToken cancellationToken)
    {
        return await _context.IndexedFiles
            .Where(x => x.LibraryId == request.LibraryId)
            .OrderBy(x => x.Path)
            .ProjectTo<IndexedFileDto>(_mapper.ConfigurationProvider)
            .PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
