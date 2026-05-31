using K7.Server.Application.Features.Federation.Queries.GetPeerRequests;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class GetPeerRequestsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/peers/requests", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var requests = await sender.Send(new GetPeerRequestsQuery(), cancellationToken);
            return Results.Ok(requests);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
