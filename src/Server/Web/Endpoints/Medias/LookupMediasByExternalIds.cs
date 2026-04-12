using K7.Server.Application.Features.Medias.Queries.LookupMediasByExternalIds;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class LookupMediasByExternalIds : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/medias/by-external-ids", async (
            [FromBody] LookupMediasByExternalIdsRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var results = await sender.Send(new LookupMediasByExternalIdsQuery
            {
                Items = request.Items
            }, cancellationToken);
            return Results.Ok(results);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
