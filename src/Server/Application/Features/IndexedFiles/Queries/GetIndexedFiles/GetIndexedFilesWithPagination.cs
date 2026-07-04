using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFiles;

public record GetIndexedFilesWithPaginationQuery : IRequest<PaginatedList<IndexedFile>>
{
    public Guid? LibraryId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class GetIndexedFilesQueryHandler : IRequestHandler<GetIndexedFilesWithPaginationQuery, PaginatedList<IndexedFile>>
{
    private readonly IApplicationDbContext _context;

    public GetIndexedFilesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<IndexedFile>> Handle(GetIndexedFilesWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.IndexedFiles.AsQueryable();

        if (request.LibraryId.HasValue)
        {
            query = query.Where(x => x.LibraryId == request.LibraryId.Value);
        }

        return await query.OrderBy(x => x.Path)
            .PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
