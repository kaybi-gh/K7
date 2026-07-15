using K7.Server.Application.Features.Federation.Commands.HandlePeerMediaNotification;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class NotifyMedia : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/media-notify", async (
            [FromBody] PeerMediaNotifyRequest body,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            await sender.Send(new HandlePeerMediaNotificationCommand(clientId, body), cancellationToken);
            return Results.Ok();
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
