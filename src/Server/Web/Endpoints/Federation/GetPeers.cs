using K7.Server.Application.Features.Federation.Queries.GetPeers;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class GetPeersEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/peers", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var peers = await sender.Send(new GetPeersQuery(), cancellationToken);
            return Results.Ok(peers);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
