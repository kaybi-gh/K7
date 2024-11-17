using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Models.Dtos;
using K7.Server.Application.Features.Medias.Queries.GetPersons;
using K7.Server.Application.Features.Persons.Queries.GetPerson;

namespace K7.Server.Web.Endpoints;

public class Persons : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetPerson, "{id}")
            .MapGet(GetPersons);
    }

    public async Task<PersonDto> GetPerson(ISender sender, Guid id)
    {
        return await sender.Send(new GetPersonQuery(id));
    }

    public async Task<PaginatedList<LitePersonDto>> GetPersons(ISender sender, [AsParameters] GetPersonsWithPaginationQuery query)
    {
        return await sender.Send(query);
    }
}
