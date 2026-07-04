using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFile;

public record GetIndexedFileQuery(Guid Id) : IRequest<IndexedFile>;

public class GetIndexedFileQueryHandler : IRequestHandler<GetIndexedFileQuery, IndexedFile>
{
    private readonly IApplicationDbContext _context;

    public GetIndexedFileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IndexedFile> Handle(GetIndexedFileQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .AsNoTracking()
            .Include(x => x.Media)
            .Where(x => x.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
