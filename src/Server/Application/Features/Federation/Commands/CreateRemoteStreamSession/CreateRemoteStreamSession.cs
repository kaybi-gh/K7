using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.CreateRemoteStreamSession;

public record CreateRemoteStreamSessionCommand(
    CreateRemoteStreamSessionRequest Request,
    Guid UserId) : IRequest<CreateRemoteStreamSessionResult>;

public record CreateRemoteStreamSessionResult(StreamingSessionDto Session, string Location);

public class CreateRemoteStreamSessionCommandHandler(
    IApplicationDbContext context,
    IPeerAuthorizationService peerAuthorization,
    IPeerClient peerClient)
    : IRequestHandler<CreateRemoteStreamSessionCommand, CreateRemoteStreamSessionResult>
{
    public async Task<CreateRemoteStreamSessionResult> Handle(
        CreateRemoteStreamSessionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;

        var remoteFile = await context.RemoteIndexedFiles
            .Include(r => r.PeerServer)
            .FirstOrDefaultAsync(r => r.Id == request.RemoteFileId, cancellationToken);

        if (remoteFile?.PeerServer is null)
            throw new NotFoundException(request.RemoteFileId.ToString(), "RemoteIndexedFile");

        var peer = remoteFile.PeerServer;
        if (peer.Status != PeerStatus.Active)
            throw new PeerServerUnavailableException("Peer server is not active");

        if (peer.LastTestSucceeded == false)
            throw new PeerServerUnavailableException("Peer server is unreachable");

        var device = await context.Devices
            .FindAsync([request.DeviceId], cancellationToken);

        if (device is null)
            throw new NotFoundException(request.DeviceId.ToString(), nameof(Domain.Entities.Devices.Device));

        var auth = await peerAuthorization.AuthenticateOutboundAsync(peer.Id, cancellationToken);
        if (auth is null)
            throw new HttpRequestException("Failed to authenticate with peer.");

        var token = auth.Value.Token;
        var capabilitiesDto = device.PlaybackCapabilities.ToDevicePlaybackCapabilitiesDto();

        var federationRequest = new CreateFederationStreamSessionRequest
        {
            IndexedFileId = remoteFile.RemoteFileId,
            DeviceCapabilities = capabilitiesDto,
            AudioTrackIndex = request.AudioTrackIndex
        };

        var remoteSession = await peerClient.CreateRemoteStreamSessionAsync(
            peer.BaseUrl, token, federationRequest, cancellationToken);

        if (remoteSession is null)
            throw new HttpRequestException("Failed to create stream session on peer.");

        var localSession = new StreamSession
        {
            Id = Guid.NewGuid(),
            RemoteIndexedFileId = remoteFile.Id,
            DeviceId = device.Id,
            UserId = command.UserId,
            PeerServerId = peer.Id,
            RemoteSessionId = remoteSession.Id,
            State = PlaybackState.Idle,
            Position = 0,
            PlaybackSettingsJson = "{}"
        };

        context.StreamSessions.Add(localSession);
        await context.SaveChangesAsync(cancellationToken);

        IndexedFileStreamUri? localSource = null;
        if (remoteSession.Source is not null)
        {
            var remotePath = remoteSession.Source.Uri.IsAbsoluteUri
                ? remoteSession.Source.Uri.PathAndQuery
                : remoteSession.Source.Uri.OriginalString;

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
            Source = localSource,
            AudioTracks = remoteSession.AudioTracks,
            SubtitleTracks = remoteSession.SubtitleTracks
        };

        return new CreateRemoteStreamSessionResult(result, $"/api/remote-stream-sessions/{localSession.Id}");
    }
}
