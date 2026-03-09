using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Medias.Queries.GetPersons;

public record GetPersonsWithPaginationQuery : IRequest<PaginatedList<Person>>
{
    public Guid[]? Ids { get; init; }
    public Guid[]? MediaIds { get; init; }
    public EnumHashSetQueryParam<PersonRoleType>? RoleTypes { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetPersonsQueryHandler : IRequestHandler<GetPersonsWithPaginationQuery, PaginatedList<Person>>
{
    private readonly IApplicationDbContext _context;

    public GetPersonsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<Person>> Handle(GetPersonsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Persons
            .Include(x => x.PortraitPicture)
            .Include(x => x.ExternalIds)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Media)
            .AsQueryable();

        query = ApplyFilters(request, query);
        return await query.PaginatedListAsync(request.PageNumber, request.PageSize);
    }

    private static IQueryable<Person> ApplyFilters(GetPersonsWithPaginationQuery request, IQueryable<Person> query)
    {
        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
        }

        if (request.MediaIds?.Length > 0)
        {
            query = query.Where(x => x.Roles.Any(x => request.MediaIds.Contains(x.MediaId)));
        }

        if (request.RoleTypes?.Count > 0)
        {
            query = query.Where(x => x.Roles.Any(r => request.RoleTypes.Contains(r.Type)));
        }

        return query;
    }
}
