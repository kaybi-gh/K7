using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Models.Dtos;

namespace MediaServer.Application.Features.Libraries.Queries.GetLibraries;

//[Authorize]
public record GetLibrariesQuery : IRequest<IEnumerable<LibraryDto>>;

public class GetLibrariesQueryHandler : IRequestHandler<GetLibrariesQuery, IEnumerable<LibraryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetLibrariesQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<LibraryDto>> Handle(GetLibrariesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Libraries
            .AsNoTracking()
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .OrderBy(t => t.Title)
            .ToListAsync(cancellationToken);
    }
}
