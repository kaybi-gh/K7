using K7.Server.Application.Features.StreamSessions.Queries.GetStreamSession;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.StreamSessions;

public sealed class GetStreamSessionHlsFile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/stream-sessions/{sessionId:guid}/hls/{*file}", async (
            [FromServices] ISender sender,
            [FromRoute] Guid sessionId,
            [FromRoute] string file,
            CancellationToken cancellationToken) =>
        {
            var sessionInfo = await sender.Send(new GetStreamSessionQuery(sessionId), cancellationToken);
            if (sessionInfo is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(file))
            {
                return Results.BadRequest();
            }

            var relativePath = file.Replace('\\', '/');
            var fullPath = Path.Combine(sessionInfo.RootDirectory, relativePath);

            // For the master manifest, allow a short wait so that the
            // background ffmpeg process has time to create both the master
            // and the underlying media playlist (index.m3u8). Serving master
            // before index exists would make the player stall on startup.
            if (relativePath.EndsWith("master.m3u8", StringComparison.OrdinalIgnoreCase))
            {
                var deadline = DateTime.UtcNow.AddSeconds(10);
                var mediaPlaylistPath = Path.Combine(sessionInfo.RootDirectory, "index.m3u8");

                while ((!File.Exists(fullPath) || !File.Exists(mediaPlaylistPath))
                    && DateTime.UtcNow < deadline
                    && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(200, cancellationToken);
                }
            }

            if (!File.Exists(fullPath))
            {
                return Results.NotFound();
            }

            var contentType = MimeTypeHelper.GetStreamContentType(Path.GetExtension(fullPath));
            var stream = File.OpenRead(fullPath);

            return Results.File(stream, contentType: contentType);
        })
        .RequireAuthorization(Policies.StreamAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
