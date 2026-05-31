using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class PingPeer : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/ping", async (
            [FromServices] IApplicationDbContext context,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            if (clientId is null)
                return Results.Forbid();

            var peer = await context.PeerServers
                .FirstOrDefaultAsync(p => p.InboundApplicationId == clientId && p.Status == PeerStatus.Active, cancellationToken);

            if (peer is null)
                return Results.Forbid();

            return Results.Ok(new { PeerName = peer.Name });
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
