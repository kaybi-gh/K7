using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
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
            var session = await sender.Send(command, cancellationToken);

            // Negotiate the initial stream (direct-play vs HLS)
            var streamUri = await sender.Send(new GetStreamUriQuery
            {
                Id = session.IndexedFileId,
                DeviceId = command.DeviceId,
                StreamSessionId = session.Id,
                AudioTrackIndex = command.AudioTrackIndex
            }, cancellationToken);

            session.Source = streamUri;
            return Results.Created($"/api/stream-sessions/{session.Id}", session);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
