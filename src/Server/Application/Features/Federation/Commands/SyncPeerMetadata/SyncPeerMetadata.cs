using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.Commands.SyncPeerMetadata;

[Authorize(Roles = Roles.Administrator)]
public record SyncPeerMetadataCommand(Guid PeerId) : IRequest;

public class SyncPeerMetadataCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    ILogger<SyncPeerMetadataCommandHandler> logger)
    : IRequestHandler<SyncPeerMetadataCommand>
{
    public async Task Handle(SyncPeerMetadataCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Id == request.PeerId && p.Status == PeerStatus.Active, cancellationToken);

        Guard.Against.NotFound(request.PeerId, peer);

        if (peer.OutboundClientId is null || peer.OutboundClientSecret is null)
        {
            logger.LogWarning("Peer {PeerName} has no outbound credentials, skipping sync", peer.Name);
            return;
        }

        var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, cancellationToken);
        if (token is null)
        {
            logger.LogWarning("Failed to get access token for peer {PeerName}", peer.Name);
            return;
        }

        var remoteLibraries = await peerClient.GetRemoteLibrariesAsync(peer.BaseUrl, token, cancellationToken);
        logger.LogInformation("Peer {PeerName} exposes {Count} libraries", peer.Name, remoteLibraries.Count);

        foreach (var remoteLibrary in remoteLibraries)
        {
            var localLibrary = await EnsureLocalLibraryAsync(peer, remoteLibrary, cancellationToken);
            var remoteMedia = await peerClient.GetRemoteMediaAsync(peer.BaseUrl, token, remoteLibrary.Id, cancellationToken);

            foreach (var media in remoteMedia)
            {
                await SyncMediaAsync(peer, localLibrary, remoteLibrary.Id, media, cancellationToken);
            }
        }

        peer.LastSeen = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Library> EnsureLocalLibraryAsync(PeerServer peer, PeerLibraryDto remoteLibrary, CancellationToken cancellationToken)
    {
        var existing = await context.Libraries
            .FirstOrDefaultAsync(l => l.PeerServerId == peer.Id && l.Title == remoteLibrary.Title, cancellationToken);

        if (existing is not null)
            return existing;

        var group = await context.LibraryGroups
            .FirstOrDefaultAsync(g => g.MediaType == remoteLibrary.MediaType, cancellationToken);

        if (group is null)
        {
            group = new LibraryGroup
            {
                Id = Guid.NewGuid(),
                Title = $"{peer.Name} - {remoteLibrary.MediaType}",
                MediaType = remoteLibrary.MediaType
            };
            context.LibraryGroups.Add(group);
        }

        var library = new Library
        {
            Id = Guid.NewGuid(),
            Title = remoteLibrary.Title,
            MediaType = remoteLibrary.MediaType,
            MetadataProviderName = "none",
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en",
            LibraryGroupId = group.Id,
            PeerServerId = peer.Id
        };

        context.Libraries.Add(library);
        return library;
    }

    private async Task SyncMediaAsync(PeerServer peer, Library localLibrary, Guid remoteLibraryId, PeerMediaDto media, CancellationToken cancellationToken)
    {
        BaseMedia? localMedia = null;

        // Try to match by ExternalId (dedup)
        foreach (var externalId in media.ExternalIds)
        {
            var match = await context.ExternalIds
                .Where(e => e.ProviderName == externalId.Provider && e.Value == externalId.Value && e.MediaId != null)
                .Select(e => e.Media)
                .FirstOrDefaultAsync(cancellationToken);

            if (match is not null)
            {
                localMedia = match;
                break;
            }
        }

        if (localMedia is null)
        {
            // No match - create a placeholder media (type-specific creation handled elsewhere)
            // For now skip creation of new media - will be refined in later iteration
            return;
        }

        // Sync RemoteIndexedFiles
        foreach (var file in media.Files)
        {
            var existingRemoteFile = await context.RemoteIndexedFiles
                .AnyAsync(r => r.PeerServerId == peer.Id && r.RemoteFileId == file.Id, cancellationToken);

            if (!existingRemoteFile)
            {
                context.RemoteIndexedFiles.Add(new RemoteIndexedFile
                {
                    Id = Guid.NewGuid(),
                    PeerServerId = peer.Id,
                    RemoteFileId = file.Id,
                    Name = file.Name,
                    Extension = file.Extension,
                    Size = file.Size,
                    MediaId = localMedia.Id,
                    RemoteMediaId = media.Id,
                    LibraryId = localLibrary.Id,
                    RemoteLibraryId = remoteLibraryId
                });
            }
        }
    }
}
