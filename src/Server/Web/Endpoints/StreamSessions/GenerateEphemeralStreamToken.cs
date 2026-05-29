using K7.Server.Application.Features.StreamSessions.Commands.GenerateEphemeralStreamToken;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.StreamSessions;

public class GenerateEphemeralStreamToken : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/stream-sessions/{sessionId:guid}/ephemeral-token", async (
            [FromServices] ISender sender,
            [FromRoute] Guid sessionId,
            CancellationToken cancellationToken) =>
        {
            var token = await sender.Send(new GenerateEphemeralStreamTokenCommand
            {
                StreamSessionId = sessionId
            }, cancellationToken);

            return Results.Ok(new { token });
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
