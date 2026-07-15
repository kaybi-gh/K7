using K7.Server.Application.Features.Federation.Queries.PingFederationPeer;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class PingPeer : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/ping", async (
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var result = await sender.Send(new PingFederationPeerQuery(clientId), cancellationToken);
            return Results.Ok(new { result.PeerName });
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
