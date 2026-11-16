using K7.Server.Application.Features.StreamSessions.Commands.CreateStreamSession;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.StreamSessions;

public class CreateStreamSession : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/stream-sessions", async (
            [FromServices] ISender sender,
            [FromBody] CreateStreamSessionCommand command,
            CancellationToken cancellationToken) =>
        {
            var session = await StreamSessionNegotiator.CreateAsync(sender, command, cancellationToken);
            return Results.Created($"/api/stream-sessions/{session.Id}", session);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
