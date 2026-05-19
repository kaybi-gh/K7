using K7.Server.Application.Features.Persons.Queries.GetPersonKnownFor;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Persons;

public class GetPersonKnownFor : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/persons/{id}/known-for", async ([FromServices] ISender sender, Guid id) =>
        {
            var result = await sender.Send(new GetPersonKnownForQuery { PersonId = id });
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
