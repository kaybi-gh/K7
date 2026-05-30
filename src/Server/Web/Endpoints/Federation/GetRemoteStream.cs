using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetRemoteStream : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/remote-stream/{remoteFileId:guid}", async (
            Guid remoteFileId,
            [FromServices] IApplicationDbContext context,
            [FromServices] IPeerClient peerClient,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var remoteFile = await context.RemoteIndexedFiles
                .Include(r => r.PeerServer)
                .FirstOrDefaultAsync(r => r.Id == remoteFileId, cancellationToken);

            if (remoteFile?.PeerServer is null)
                return Results.NotFound();

            var peer = remoteFile.PeerServer;
            if (peer.Status != PeerStatus.Active)
                return Results.Problem("Peer server is not active", statusCode: 503);

            var token = await peerClient.GetAccessTokenAsync(
                peer.BaseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);

            if (token is null)
                return Results.Problem("Failed to authenticate with peer", statusCode: 502);

            var stream = await peerClient.GetRemoteStreamAsync(
                peer.BaseUrl, token, remoteFile.RemoteFileId, cancellationToken);

            if (stream is null)
                return Results.Problem("Failed to retrieve stream from peer", statusCode: 502);

            var extension = remoteFile.Extension?.TrimStart('.');
            var mimeType = extension is not null
                && K7.Server.Domain.Constants.Constants.ContainerMimeTypeMapping.TryGetValue(extension, out var mime)
                    ? mime
                    : "application/octet-stream";

            return Results.Stream(stream, contentType: mimeType, enableRangeProcessing: true);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
