using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Queries.GetRemoteMetadataPicture;

public record GetRemoteMetadataPictureQuery(
    Guid RemoteFileId,
    Guid PictureId,
    MetadataPictureSize? Size = null) : IRequest<RemoteMetadataPictureProxyResult>;

public record RemoteMetadataPictureProxyResult(
    int StatusCode,
    Stream? Body,
    string? ContentType,
    long? ContentLength,
    IReadOnlyDictionary<string, string[]> ForwardHeaders);

public class GetRemoteMetadataPictureQueryHandler(
    IApplicationDbContext context,
    IPeerAuthorizationService peerAuthorization,
    IPeerClient peerClient)
    : IRequestHandler<GetRemoteMetadataPictureQuery, RemoteMetadataPictureProxyResult>
{
    public async Task<RemoteMetadataPictureProxyResult> Handle(
        GetRemoteMetadataPictureQuery request,
        CancellationToken cancellationToken)
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

        var response = await peerClient.GetRemoteMetadataPictureAsync(
            peer.BaseUrl,
            token,
            request.PictureId,
            request.Size,
            cancellationToken);

        var forwardHeaders = new Dictionary<string, string[]>();
        foreach (var header in response.Headers)
        {
            if (header.Key.Equals("ETag", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Cache-Control", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase))
            {
                forwardHeaders[header.Key] = header.Value.ToArray();
            }
        }

        Stream? body = null;
        if (response.IsSuccessStatusCode)
            body = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new RemoteMetadataPictureProxyResult(
            (int)response.StatusCode,
            body,
            response.Content.Headers.ContentType?.ToString(),
            response.Content.Headers.ContentLength,
            forwardHeaders);
    }
}
