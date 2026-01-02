using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibrary;

[Authorize]
public record GetLibraryQuery(Guid Id) : IRequest<Library>;

public class GetLibraryQueryHandler : IRequestHandler<GetLibraryQuery, Library>
{
    private readonly IApplicationDbContext _context;

    public GetLibraryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Library> Handle(GetLibraryQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
