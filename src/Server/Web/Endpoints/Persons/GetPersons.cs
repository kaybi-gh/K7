using K7.Server.Application.Features.Medias.Queries.GetPersons;
using K7.Server.Web.Converters;
using K7.Shared.Dtos.Entities.Persons;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Persons;

public class GetPersons : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/persons", async ([FromServices] ISender sender, [AsParameters] GetPersonsWithPaginationQuery query, CancellationToken cancellationToken) =>
        {
            var personsPage = await sender.Send(query, cancellationToken);
            return personsPage.ToDto(PersonDto.FromDomain);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
