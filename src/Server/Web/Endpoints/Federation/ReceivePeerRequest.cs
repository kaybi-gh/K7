using K7.Server.Application.Features.Federation.Commands.ReceivePeerRequest;
using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

namespace K7.Server.Web.Endpoints.Federation;

public class ReceivePeerRequestEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peer-request", async (
            [Microsoft.AspNetCore.Mvc.FromBody] ReceivePeerRequestCommand command,
            [Microsoft.AspNetCore.Mvc.FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(command, cancellationToken);
            return Results.Ok();
        })
        .RequireRateLimiting(RateLimitingExtensions.FederationInvitationPolicy)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
