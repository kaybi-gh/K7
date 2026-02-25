using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Features.StreamSessions;
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
                DeviceId = command.DeviceId
            }, cancellationToken);

            // For both direct-play and HLS cases, propagate the negotiated URI.
            // When HLS is selected, this points to GetHlsStreamManifest, which
            // generates the master playlist on demand based on the indexed file
            // and device capabilities.
            session.Source = streamUri;
            return Results.Created($"/api/stream-sessions/{session.Id}", session);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
