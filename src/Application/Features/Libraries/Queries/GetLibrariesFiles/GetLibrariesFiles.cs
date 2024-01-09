using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Security;

namespace MediaServer.Application.Features.Libraries.Queries.GetLibrariesFiles;

[Authorize]
public record GetLibraryFilesQuery : IRequest<IEnumerable<LibraryFilesDto>>;

public class GetLibraryFilesQueryHandler : IRequestHandler<GetLibraryFilesQuery, IEnumerable<LibraryFilesDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetLibraryFilesQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<LibraryFilesDto>> Handle(GetLibraryFilesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Libraries
            .AsNoTracking()
            .ProjectTo<LibraryFilesDto>(_mapper.ConfigurationProvider)
            .OrderBy(t => t.Title)
            .ToListAsync(cancellationToken);
    }
}
