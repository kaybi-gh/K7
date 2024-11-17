using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models.Dtos;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibraries;

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
