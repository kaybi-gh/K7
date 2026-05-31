using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetRemoteFileDetails : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/remote-indexed-files/{remoteFileId:guid}/details", async (
            Guid remoteFileId,
            [FromServices] IApplicationDbContext context,
            [FromServices] IPeerClient peerClient,
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

            var fileDetails = await peerClient.GetRemoteFileDetailsAsync(
                peer.BaseUrl, token, remoteFile.RemoteFileId, cancellationToken);

            if (fileDetails is null)
                return Results.NotFound();

            return Results.Ok(fileDetails);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
