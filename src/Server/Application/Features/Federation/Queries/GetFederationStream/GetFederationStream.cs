using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationStream;

public record GetFederationStreamQuery(string? ClientId, Guid FileId) : IRequest<FederationStreamFileResult>;

public record FederationStreamFileResult(string Path, string MimeType);

public class GetFederationStreamQueryHandler(IPeerAuthorizationService peerAuthorization)
    : IRequestHandler<GetFederationStreamQuery, FederationStreamFileResult>
{
    public async Task<FederationStreamFileResult> Handle(
        GetFederationStreamQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);
        var indexedFile = await peerAuthorization.RequireFileAccessibleToPeerAsync(peer.Id, request.FileId, cancellationToken);

        await peerAuthorization.EnsureConcurrentStreamQuotaAsync(peer.Id, indexedFile.LibraryId, cancellationToken);

        var file = new FileInfo(indexedFile.Path);
        if (!file.Exists)
            throw new NotFoundException(request.FileId.ToString(), nameof(Domain.Entities.IndexedFile));

        var container = indexedFile.FileMetadata?.Container;
        var mimeType = container is not null
            && Domain.Constants.Constants.ContainerMimeTypeMapping.TryGetValue(container, out var mime)
                ? mime
                : "application/octet-stream";

        return new FederationStreamFileResult(indexedFile.Path, mimeType);
    }
}
