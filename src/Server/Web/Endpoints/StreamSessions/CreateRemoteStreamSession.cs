using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.StreamSessions;

public class CreateRemoteStreamSession : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/remote-stream-sessions", async (
            [FromBody] CreateRemoteStreamSessionRequest request,
            [FromServices] IApplicationDbContext context,
            [FromServices] IPeerClient peerClient,
            [FromServices] IUser user,
            CancellationToken cancellationToken) =>
        {
            var remoteFile = await context.RemoteIndexedFiles
                .Include(r => r.PeerServer)
                .FirstOrDefaultAsync(r => r.Id == request.RemoteFileId, cancellationToken);

            if (remoteFile?.PeerServer is null)
                return Results.NotFound();

            var peer = remoteFile.PeerServer;
            if (peer.Status != PeerStatus.Active)
                return Results.Problem("Peer server is not active", statusCode: 503);

            var device = await context.Devices
                .FindAsync([request.DeviceId], cancellationToken);

            if (device is null)
                return Results.NotFound();

            // Authenticate with peer
            var token = await peerClient.GetAccessTokenAsync(
                peer.BaseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);

            if (token is null)
                return Results.Problem("Failed to authenticate with peer", statusCode: 502);

            // Build device capabilities DTO from local device
            var capabilitiesDto = device.PlaybackCapabilities.ToDevicePlaybackCapabilitiesDto();

            // Create stream session on the remote peer
            var federationRequest = new CreateFederationStreamSessionRequest
            {
                IndexedFileId = remoteFile.RemoteFileId,
                DeviceCapabilities = capabilitiesDto,
                AudioTrackIndex = request.AudioTrackIndex
            };

            var remoteSession = await peerClient.CreateRemoteStreamSessionAsync(
                peer.BaseUrl, token, federationRequest, cancellationToken);

            if (remoteSession is null)
                return Results.Problem("Failed to create stream session on peer", statusCode: 502);

            // Create local session to track the remote session
            var localSession = new StreamSession
            {
                Id = Guid.NewGuid(),
                RemoteIndexedFileId = remoteFile.Id,
                DeviceId = device.Id,
                UserId = user.Id,
                PeerServerId = peer.Id,
                RemoteSessionId = remoteSession.Id,
                State = PlaybackState.Idle,
                Position = 0,
                PlaybackSettingsJson = "{}"
            };

            context.StreamSessions.Add(localSession);
            await context.SaveChangesAsync(cancellationToken);

            // Rewrite the source URI to point to the local proxy
            IndexedFileStreamUri? localSource = null;
            if (remoteSession.Source is not null)
            {
                var remotePath = remoteSession.Source.Uri.IsAbsoluteUri
                    ? remoteSession.Source.Uri.PathAndQuery
                    : remoteSession.Source.Uri.OriginalString;

                // The remote source points to /api/indexed-files/{id}/... 
                // We rewrite it to /api/remote-stream-sessions/{localSessionId}/{path}
                var indexedFilePath = $"/api/indexed-files/{remoteFile.RemoteFileId}/";
                string proxyPath;

                if (remotePath.Contains(indexedFilePath))
                {
                    var relativePath = remotePath[(remotePath.IndexOf(indexedFilePath) + indexedFilePath.Length)..];
                    proxyPath = $"/api/remote-stream-sessions/{localSession.Id}/{relativePath}";
                }
                else
                {
                    proxyPath = $"/api/remote-stream-sessions/{localSession.Id}/direct-stream";
                }

                localSource = new IndexedFileStreamUri
                {
                    Uri = new Uri(proxyPath, UriKind.Relative),
                    MimeType = remoteSession.Source.MimeType
                };
            }

            var result = new StreamingSessionDto
            {
                Id = localSession.Id,
                IndexedFileId = remoteFile.Id,
                State = localSession.State,
                Position = localSession.Position,
                PlaybackSettings = remoteSession.PlaybackSettings ?? new PlaybackSettingsDto(),
                Source = localSource
            };

            return Results.Created($"/api/remote-stream-sessions/{localSession.Id}", result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
