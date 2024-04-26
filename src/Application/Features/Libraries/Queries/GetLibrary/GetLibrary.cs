using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Security;

namespace MediaServer.Application.Features.Libraries.Queries.GetLibrary;

[Authorize]
public record GetLibraryQuery(Guid Id) : IRequest<LibraryDto>;

public class GetLibraryQueryHandler : IRequestHandler<GetLibraryQuery, LibraryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetLibraryQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<LibraryDto> Handle(GetLibraryQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
