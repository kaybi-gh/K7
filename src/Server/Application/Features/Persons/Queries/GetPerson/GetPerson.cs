using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Metadatas;

namespace K7.Server.Application.Features.Persons.Queries.GetPerson;

public record GetPersonQuery(Guid Id) : IRequest<Person>;

public class GetPersonQueryHandler : IRequestHandler<GetPersonQuery, Person>
{
    private readonly IApplicationDbContext _context;

    public GetPersonQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Person> Handle(GetPersonQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Persons
        .AsNoTracking()
        .Include(x => x.ExternalIds)
        .Include(x => x.PortraitPicture)
        .Include(x => x.Roles)
            .ThenInclude(x => x.Media)
        .Include(x => x.Roles)
            .ThenInclude(x => x.Media)
                .ThenInclude(x => x.Pictures)
        .Where(x => x.Id == request.Id)
        .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
