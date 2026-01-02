using K7.Server.Application.Features.Persons.Queries.GetPerson;
using K7.Shared.Dtos.Entities.Persons;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Persons;

public class GetPerson : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/persons/{id}", async ([FromServices] ISender sender, Guid id) =>
        {
            var person = await sender.Send(new GetPersonQuery(id));
            return PersonDto.FromDomain(person);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
