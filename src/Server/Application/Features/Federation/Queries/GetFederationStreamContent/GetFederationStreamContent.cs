using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Queries.GetFederationStream;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationStreamContent;

public record GetFederationStreamSessionQuery(string? ClientId, Guid SessionId) : IRequest<FederationStreamSessionResult>;

public record FederationStreamSessionResult(Guid IndexedFileId);

public class GetFederationStreamSessionQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<GetFederationStreamSessionQuery, FederationStreamSessionResult>
{
    public async Task<FederationStreamSessionResult> Handle(
        GetFederationStreamSessionQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);

        var session = await context.StreamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.PeerServerId == peer.Id, cancellationToken);

        if (session?.IndexedFileId is null)
            throw new NotFoundException(request.SessionId.ToString(), "StreamSession");

        return new FederationStreamSessionResult(session.IndexedFileId.Value);
    }
}

public record GetFederationDirectStreamQuery(Guid IndexedFileId) : IRequest<FederationStreamFileResult>;

public class GetFederationDirectStreamQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetFederationDirectStreamQuery, FederationStreamFileResult>
{
    public async Task<FederationStreamFileResult> Handle(
        GetFederationDirectStreamQuery request,
        CancellationToken cancellationToken)
    {
        var indexedFile = await context.IndexedFiles
            .FirstOrDefaultAsync(f => f.Id == request.IndexedFileId, cancellationToken);

        if (indexedFile is null || !File.Exists(indexedFile.Path))
            throw new NotFoundException(request.IndexedFileId.ToString(), nameof(Domain.Entities.IndexedFile));

        var container = indexedFile.FileMetadata?.Container;
        var mimeType = container is not null
            && Domain.Constants.Constants.ContainerMimeTypeMapping.TryGetValue(container, out var mime)
                ? mime
                : "application/octet-stream";

        return new FederationStreamFileResult(indexedFile.Path, mimeType);
    }
}
