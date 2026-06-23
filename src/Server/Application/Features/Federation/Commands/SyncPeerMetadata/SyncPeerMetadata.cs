using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
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
    ISender sender,
    IMediaMetadataTagSyncService tagSyncService,
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

            var agreement = await context.PeerShareAgreements
                .FirstOrDefaultAsync(a => a.PeerServerId == peer.Id
                    && a.LibraryId == localLibrary.Id
                    && a.Direction == ShareDirection.Inbound, cancellationToken);

            if (agreement is null)
            {
                agreement = new PeerShareAgreement
                {
                    Id = Guid.NewGuid(),
                    PeerServerId = peer.Id,
                    LibraryId = localLibrary.Id,
                    Direction = ShareDirection.Inbound,
                    IsEnabled = true
                };
                context.PeerShareAgreements.Add(agreement);
                await context.SaveChangesAsync(cancellationToken);
            }

            if (!agreement.IsEnabled)
                continue;

            var remoteMedia = await peerClient.GetRemoteMediaAsync(peer.BaseUrl, token, remoteLibrary.Id, cancellationToken);

            foreach (var media in remoteMedia)
            {
                await SyncMediaAsync(peer, localLibrary, remoteLibrary.Id, media, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        // Remove inbound agreements for libraries no longer shared by the provider
        var remoteLibraryTitles = remoteLibraries.Select(r => r.Title).ToHashSet();

        var staleAgreements = await context.PeerShareAgreements
            .Include(a => a.Library)
            .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Inbound)
            .ToListAsync(cancellationToken);

        var agreementsToRemove = staleAgreements
            .Where(a => a.Library is not null && !remoteLibraryTitles.Contains(a.Library.Title))
            .ToList();

        if (agreementsToRemove.Count > 0)
        {
            var removedLibraryIds = agreementsToRemove.Select(a => a.LibraryId).ToList();

            // Remove remote indexed files belonging to the removed libraries
            await context.RemoteIndexedFiles
                .Where(r => r.PeerServerId == peer.Id && removedLibraryIds.Contains(r.LibraryId))
                .ExecuteDeleteAsync(cancellationToken);

            context.PeerShareAgreements.RemoveRange(agreementsToRemove);

            // Remove the local mirror libraries themselves
            var librariesToRemove = await context.Libraries
                .Where(l => removedLibraryIds.Contains(l.Id) && l.PeerServerId == peer.Id)
                .ToListAsync(cancellationToken);

            context.Libraries.RemoveRange(librariesToRemove);

            logger.LogInformation("Removed {Count} stale inbound agreements from peer {PeerName} (libraries no longer shared)", agreementsToRemove.Count, peer.Name);
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
            MetadataProviderName = "federation",
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

        // Try to match by ExternalId (dedup against existing local media)
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
            // Also check if we already created this media from a previous sync
            var federationExternalIdValue = $"{peer.Id}:{media.Id}";
            localMedia = await context.ExternalIds
                .Where(e => e.ProviderName == "federation" && e.Value == federationExternalIdValue && e.MediaId != null)
                .Select(e => e.Media)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (localMedia is null)
        {
            localMedia = CreateMedia(media.Type);
            localMedia.Title = media.Title;
            localMedia.OriginalTitle = media.OriginalTitle;
            localMedia.ReleaseDate = media.ReleaseDate;
            localMedia.PeerServerId = peer.Id;

            await tagSyncService.ApplyTagsAsync(
                localMedia,
                MetadataTagBuilder.FromGenres(localMedia, media.Genres),
                cancellationToken);

            // Add federation external ID for identification
            localMedia.ExternalIds.Add(new ExternalId
            {
                ProviderName = "federation",
                Value = $"{peer.Id}:{media.Id}"
            });

            // Copy all remote external IDs for future local match
            foreach (var externalId in media.ExternalIds)
            {
                localMedia.ExternalIds.Add(new ExternalId
                {
                    ProviderName = externalId.Provider,
                    Value = externalId.Value
                });
            }

            context.Medias.Add(localMedia);
            logger.LogInformation("Created federated media {Title} ({Type}) from peer {PeerName}", media.Title, media.Type, peer.Name);

            // Enqueue full metadata refresh from peer
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshMediaMetadatasCommand
                {
                    MediaId = localMedia.Id,
                    MetadataProviderExternalId = $"{peer.Id}:{media.Id}",
                    MetadataProviderName = "federation",
                    Language = localLibrary.MetadataLanguage,
                    FallbackLanguage = localLibrary.MetadataFallbackLanguage
                },
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = localMedia.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                MaxAttempts = 3,
                ConcurrencyGroup = $"federation:{peer.Id}"
            }, cancellationToken);
        }
        else
        {
            // Ensure federation external ID exists on matched local media
            var federationValue = $"{peer.Id}:{media.Id}";
            var hasFederationId = await context.ExternalIds
                .AnyAsync(e => e.ProviderName == "federation" && e.Value == federationValue && e.MediaId == localMedia.Id, cancellationToken);

            if (!hasFederationId)
            {
                context.ExternalIds.Add(new ExternalId
                {
                    ProviderName = "federation",
                    Value = federationValue,
                    MediaId = localMedia.Id
                });
            }
        }

        // Sync RemoteIndexedFiles
        foreach (var file in media.Files)
        {
            var existingRemoteFile = await context.RemoteIndexedFiles
                .FirstOrDefaultAsync(r => r.PeerServerId == peer.Id && r.RemoteFileId == file.Id, cancellationToken);

            if (existingRemoteFile is null)
            {
                context.RemoteIndexedFiles.Add(new RemoteIndexedFile
                {
                    Id = Guid.NewGuid(),
                    PeerServerId = peer.Id,
                    RemoteFileId = file.Id,
                    Name = file.Name,
                    Extension = file.Extension,
                    Size = file.Size,
                    Container = file.Container,
                    Duration = file.Duration,
                    VideoBitrate = file.VideoBitrate,
                    VideoResolution = file.VideoResolution,
                    MediaId = localMedia.Id,
                    RemoteMediaId = file.MediaId ?? media.Id,
                    LibraryId = localLibrary.Id,
                    RemoteLibraryId = remoteLibraryId
                });
            }
            else if (existingRemoteFile.MediaId != localMedia.Id)
            {
                // Re-parent file to the correct top-level entity (e.g., album instead of track)
                existingRemoteFile.MediaId = localMedia.Id;
                existingRemoteFile.RemoteMediaId = file.MediaId ?? media.Id;
            }
        }
    }

    private static BaseMedia CreateMedia(MediaType type) => type switch
    {
        MediaType.Movie => new Movie(),
        MediaType.Serie => new Serie(),
        MediaType.MusicAlbum => new MusicAlbum(),
        MediaType.MusicArtist => new MusicArtist(),
        MediaType.MusicTrack => new MusicTrack(),
        MediaType.SerieEpisode => new SerieEpisode(),
        MediaType.SerieSeason => new SerieSeason(),
        _ => new Movie()
    };
}
