using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Persons.Queries.GetPerson;
using K7.Server.Domain.Constants;
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
            return person.ToPersonDto();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
