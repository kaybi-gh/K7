using K7.Server.Application.Features.StreamSessions.Commands.RevokeEphemeralStreamToken;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.StreamSessions;

public class RevokeEphemeralStreamToken : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/stream-sessions/{sessionId:guid}/ephemeral-token", async (
            [FromServices] ISender sender,
            [FromRoute] Guid sessionId,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RevokeEphemeralStreamTokenCommand
            {
                StreamSessionId = sessionId
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
