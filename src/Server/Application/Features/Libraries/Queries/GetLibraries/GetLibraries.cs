using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibraries;

//[Authorize]
public record GetLibrariesQuery : IRequest<IEnumerable<Library>>;

public class GetLibrariesQueryHandler : IRequestHandler<GetLibrariesQuery, IEnumerable<Library>>
{
    private readonly IApplicationDbContext _context;

    public GetLibrariesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Library>> Handle(GetLibrariesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Libraries
            .AsNoTracking()
            .OrderBy(t => t.Title)
            .ToListAsync(cancellationToken);
    }
}
