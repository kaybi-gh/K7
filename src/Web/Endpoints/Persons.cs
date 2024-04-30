using MediaServer.Application.Features.Persons.Queries.GetPerson;

namespace MediaServer.Web.Endpoints;

public class Persons : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetPerson, "{id}")
            /*.MapGet(GetPersons)*/;
    }

    public async Task<PersonDto> GetPerson(ISender sender, Guid id)
    {
        return await sender.Send(new GetPersonQuery(id));
    }

    //public async Task<PaginatedList<LitePersonDto>> GetPersons(ISender sender, [AsParameters] GetPersonsWithPaginationQuery query)
    //{
    //    return await sender.Send(query);
    //}
}
