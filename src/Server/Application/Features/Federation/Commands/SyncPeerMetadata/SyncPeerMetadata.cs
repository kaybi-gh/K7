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
using Microsoft.EntityFrameworkCore;
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
            }

            if (!agreement.IsEnabled)
                continue;

            if (HasPendingChanges())
                await context.SaveChangesAsync(cancellationToken);

            var remoteMedia = await peerClient.GetRemoteMediaAsync(peer.BaseUrl, token, remoteLibrary.Id, cancellationToken);
            await SyncLibraryMediaAsync(peer, localLibrary, remoteLibrary.Id, remoteMedia, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            ClearChangeTracker();
        }

        var remoteLibraryTitles = remoteLibraries.Select(r => r.Title).ToHashSet();

        var agreementsToRemove = await context.PeerShareAgreements
            .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Inbound)
            .Where(a => context.Libraries.Any(l => l.Id == a.LibraryId && !remoteLibraryTitles.Contains(l.Title)))
            .ToListAsync(cancellationToken);

        if (agreementsToRemove.Count > 0)
        {
            var removedLibraryIds = agreementsToRemove.Select(a => a.LibraryId).ToList();

            await context.RemoteIndexedFiles
                .Where(r => r.PeerServerId == peer.Id && removedLibraryIds.Contains(r.LibraryId))
                .ExecuteDeleteAsync(cancellationToken);

            context.PeerShareAgreements.RemoveRange(agreementsToRemove);

            var librariesToRemove = await context.Libraries
                .Where(l => removedLibraryIds.Contains(l.Id) && l.PeerServerId == peer.Id)
                .ToListAsync(cancellationToken);

            context.Libraries.RemoveRange(librariesToRemove);

            logger.LogInformation("Removed {Count} stale inbound agreements from peer {PeerName} (libraries no longer shared)", agreementsToRemove.Count, peer.Name);
        }

        peer.LastSeen = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncLibraryMediaAsync(
        PeerServer peer,
        Library localLibrary,
        Guid remoteLibraryId,
        IReadOnlyList<PeerMediaDto> remoteMedia,
        CancellationToken cancellationToken)
    {
        if (remoteMedia.Count == 0)
            return;

        var federationKeys = remoteMedia.Select(m => BuildFederationExternalIdValue(peer.Id, m.Id)).ToList();
        var externalIdValues = remoteMedia
            .SelectMany(m => m.ExternalIds.Select(e => e.Value))
            .Distinct()
            .ToList();
        var remoteFileIds = remoteMedia
            .SelectMany(m => m.Files.Select(f => f.Id))
            .Distinct()
            .ToList();

        var federationMatches = await context.ExternalIds
            .Where(e => e.ProviderName == FederationProviderName && federationKeys.Contains(e.Value) && e.MediaId != null)
            .Select(e => new { e.Value, Media = e.Media! })
            .ToListAsync(cancellationToken);

        var mediaByFederationValue = federationMatches.ToDictionary(x => x.Value, x => x.Media);

        var externalIdCandidates = externalIdValues.Count == 0
            ? []
            : await context.ExternalIds
                .Where(e => e.MediaId != null && externalIdValues.Contains(e.Value))
                .Select(e => new { e.ProviderName, e.Value, Media = e.Media! })
                .ToListAsync(cancellationToken);

        var externalIdKeySet = remoteMedia
            .SelectMany(m => m.ExternalIds.Select(e => (Provider: e.Provider, Value: e.Value)))
            .ToHashSet();

        var mediaByExternalKey = externalIdCandidates
            .Where(e => externalIdKeySet.Contains((e.ProviderName, e.Value)))
            .GroupBy(e => (e.ProviderName, e.Value))
            .ToDictionary(g => g.Key, g => g.First().Media);

        var remoteFilesByRemoteId = remoteFileIds.Count == 0
            ? new Dictionary<Guid, RemoteIndexedFile>()
            : await context.RemoteIndexedFiles
                .Where(r => r.PeerServerId == peer.Id && remoteFileIds.Contains(r.RemoteFileId))
                .ToDictionaryAsync(r => r.RemoteFileId, cancellationToken);

        var federationIdsOnMedia = federationMatches
            .Where(x => x.Media.Id != Guid.Empty)
            .Select(x => x.Value)
            .ToHashSet();

        foreach (var media in remoteMedia)
        {
            var federationValue = BuildFederationExternalIdValue(peer.Id, media.Id);
            var localMedia = ResolveLocalMedia(media, mediaByExternalKey, mediaByFederationValue, federationValue);

            if (localMedia is null)
            {
                localMedia = CreateMedia(media.Type);
                localMedia.Title = media.Title;
                localMedia.SortTitle = media.SortTitle;
                localMedia.OriginalTitle = media.OriginalTitle;
                localMedia.ReleaseDate = media.ReleaseDate;
                localMedia.PeerServerId = peer.Id;

                await tagSyncService.ApplyTagsAsync(
                    localMedia,
                    MetadataTagBuilder.FromGenres(localMedia, media.Genres),
                    cancellationToken);

                localMedia.ExternalIds.Add(new ExternalId
                {
                    ProviderName = FederationProviderName,
                    Value = federationValue
                });

                foreach (var externalId in media.ExternalIds)
                {
                    localMedia.ExternalIds.Add(new ExternalId
                    {
                        ProviderName = externalId.Provider,
                        Value = externalId.Value
                    });
                }

                context.Medias.Add(localMedia);
                mediaByFederationValue[federationValue] = localMedia;
                federationIdsOnMedia.Add(federationValue);

                logger.LogInformation("Created federated media {Title} ({Type}) from peer {PeerName}", media.Title, media.Type, peer.Name);

                await sender.Send(new CreateBackgroundTaskCommand
                {
                    Request = new RefreshMediaMetadatasCommand
                    {
                        MediaId = localMedia.Id,
                        MetadataProviderExternalId = federationValue,
                        MetadataProviderName = FederationProviderName,
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
            else if (!federationIdsOnMedia.Contains(federationValue))
            {
                context.ExternalIds.Add(new ExternalId
                {
                    ProviderName = FederationProviderName,
                    Value = federationValue,
                    MediaId = localMedia.Id
                });
                federationIdsOnMedia.Add(federationValue);
            }

            foreach (var file in media.Files)
            {
                if (!remoteFilesByRemoteId.TryGetValue(file.Id, out var existingRemoteFile))
                {
                    var remoteFile = new RemoteIndexedFile
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
                    };

                    context.RemoteIndexedFiles.Add(remoteFile);
                    remoteFilesByRemoteId[file.Id] = remoteFile;
                    continue;
                }

                if (existingRemoteFile.MediaId != localMedia.Id)
                {
                    existingRemoteFile.MediaId = localMedia.Id;
                    existingRemoteFile.RemoteMediaId = file.MediaId ?? media.Id;
                }
            }
        }
    }

    private static BaseMedia? ResolveLocalMedia(
        PeerMediaDto media,
        IReadOnlyDictionary<(string Provider, string Value), BaseMedia> mediaByExternalKey,
        IReadOnlyDictionary<string, BaseMedia> mediaByFederationValue,
        string federationValue)
    {
        foreach (var externalId in media.ExternalIds)
        {
            if (mediaByExternalKey.TryGetValue((externalId.Provider, externalId.Value), out var match))
                return match;
        }

        return mediaByFederationValue.GetValueOrDefault(federationValue);
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
            MetadataProviderName = FederationProviderName,
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en",
            LibraryGroupId = group.Id,
            PeerServerId = peer.Id
        };

        context.Libraries.Add(library);
        return library;
    }

    private bool HasPendingChanges() =>
        context is DbContext dbContext && dbContext.ChangeTracker.HasChanges();

    private void ClearChangeTracker()
    {
        if (context is DbContext dbContext)
            dbContext.ChangeTracker.Clear();
    }

    private static string BuildFederationExternalIdValue(Guid peerId, Guid mediaId) => $"{peerId}:{mediaId}";

    private const string FederationProviderName = "federation";

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
