using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Models.Dtos;

namespace MediaServer.Application.Features.Persons.Queries.GetPerson;

public record GetPersonQuery(Guid Id) : IRequest<PersonDto>;

public class GetPersonQueryHandler : IRequestHandler<GetPersonQuery, PersonDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetPersonQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PersonDto> Handle(GetPersonQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Persons
        .AsNoTracking()
        .Include(x => x.ExternalIds)
        .Include(x => x.PortraitPicture)
        .Include(x => x.Roles)
            .ThenInclude(x => x.Metadata)
                .ThenInclude(x => x.Media)
        .Include(x => x.Roles)
            .ThenInclude(x => x.Metadata)
                .ThenInclude(x => x.Pictures)
        .Where(x => x.Id == request.Id)
        .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return _mapper.Map<PersonDto>(entity);
    }
}
