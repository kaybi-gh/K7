using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Mappings;
using MediaServer.Application.Common.Models;
using MediaServer.Application.Features.Persons.Queries.GetPerson;
using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Application.Features.Medias.Queries.GetPersons;

public record GetPersonsWithPaginationQuery : IRequest<PaginatedList<LitePersonDto>>
{
    public Guid[]? Ids { get; init; }
    public Guid[]? MediaIds { get; init; }
    // TODO - public bool? Seen { get; init; }
    //public EnumHashSetQueryParam<PersonJob>? JobTypes { get; init; }
    //public EnumHashSetQueryParam<PersonOrderingOption>? OrderBy { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetPersonsQueryHandler : IRequestHandler<GetPersonsWithPaginationQuery, PaginatedList<LitePersonDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetPersonsQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedList<LitePersonDto>> Handle(GetPersonsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Persons
            .Include(x => x.PortraitPicture)
            .Include(x => x.ExternalIds)
            .Include(x => x.Roles)
                .ThenInclude(x => x!.Metadata)
                    .ThenInclude(x => x!.Media)
            .AsQueryable();

        query = ApplyFilters(request, query);
        var persons = await query.PaginatedListAsync(request.PageNumber, request.PageSize);

        List<LitePersonDto> personDtos = persons.Items
            .Select(_mapper.Map<LitePersonDto>)
            .ToList();

        return new PaginatedList<LitePersonDto>(personDtos.AsReadOnly(), persons.TotalCount, request.PageNumber, request.PageSize);
    }

    private static IQueryable<Person> ApplyFilters(GetPersonsWithPaginationQuery request, IQueryable<Person> query)
    {
        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
        }

        if (request.MediaIds?.Length > 0)
        {
            query = query.Where(x => x.Roles.Any(x => request.MediaIds.Contains(x.Metadata.MediaId)));
        }

        return query;
    }
}
