using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Federation.Queries.GetRemoteFileDetails;

public record GetRemoteFileDetailsQuery(Guid RemoteFileId) : IRequest<IndexedFileDto>;

public class GetRemoteFileDetailsQueryHandler(
    IApplicationDbContext context,
    IPeerAuthorizationService peerAuthorization,
    IPeerClient peerClient)
    : IRequestHandler<GetRemoteFileDetailsQuery, IndexedFileDto>
{
    public async Task<IndexedFileDto> Handle(GetRemoteFileDetailsQuery request, CancellationToken cancellationToken)
    {
        var remoteFile = await context.RemoteIndexedFiles
            .Include(r => r.PeerServer)
            .FirstOrDefaultAsync(r => r.Id == request.RemoteFileId, cancellationToken);

        if (remoteFile?.PeerServer is null)
            throw new NotFoundException(request.RemoteFileId.ToString(), "RemoteIndexedFile");

        if (remoteFile.PeerServer.Status != PeerStatus.Active)
            throw new PeerServerUnavailableException("Peer server is not active");

        var auth = await peerAuthorization.AuthenticateOutboundAsync(remoteFile.PeerServerId, cancellationToken);
        if (auth is null)
            throw new HttpRequestException("Failed to authenticate with peer.");

        var (peer, token) = auth.Value;

        var fileDetails = await peerClient.GetRemoteFileDetailsAsync(
            peer.BaseUrl, token, remoteFile.RemoteFileId, cancellationToken);

        if (fileDetails is null)
            throw new NotFoundException(remoteFile.RemoteFileId.ToString(), "RemoteIndexedFile");

        return fileDetails;
    }
}
