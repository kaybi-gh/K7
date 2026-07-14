using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationIndexedFileDetails;

public record GetFederationIndexedFileDetailsQuery(string? ClientId, Guid FileId) : IRequest<IndexedFileDto>;

public class GetFederationIndexedFileDetailsQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<GetFederationIndexedFileDetailsQuery, IndexedFileDto>
{
    public async Task<IndexedFileDto> Handle(
        GetFederationIndexedFileDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);

        var indexedFile = await context.IndexedFiles
            .Include(f => f.FileMetadata)
            .FirstOrDefaultAsync(f => f.Id == request.FileId, cancellationToken);

        if (indexedFile is null)
            throw new NotFoundException(request.FileId.ToString(), nameof(Domain.Entities.IndexedFile));

        if (indexedFile.FileMetadata is not null)
        {
            var entry = context.Entry(indexedFile.FileMetadata);
            if (indexedFile.FileMetadata is VideoFileMetadata)
            {
                await entry.Collection("AudioTracks").LoadAsync(cancellationToken);
                await entry.Collection("VideoTracks").LoadAsync(cancellationToken);
                await entry.Collection("SubtitleTracks").LoadAsync(cancellationToken);
            }
            else if (indexedFile.FileMetadata is AudioFileMetadata)
            {
                await entry.Reference("AudioTrack").LoadAsync(cancellationToken);
            }
        }

        var isShared = await context.PeerShareAgreements
            .AnyAsync(a => a.PeerServerId == peer.Id && a.LibraryId == indexedFile.LibraryId, cancellationToken);

        if (!isShared)
            throw new ForbiddenAccessException();

        return indexedFile.ToIndexedFileDto();
    }
}
