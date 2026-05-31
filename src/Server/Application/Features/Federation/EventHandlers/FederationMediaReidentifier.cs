using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.EventHandlers;

public class FederationMediaReidentifier(
    IApplicationDbContext context,
    ILogger<FederationMediaReidentifier> logger)
    : INotificationHandler<MediaMetadataRefreshedEvent>
{
    public async Task Handle(MediaMetadataRefreshedEvent notification, CancellationToken cancellationToken)
    {
        var media = notification.Media;

        // Only run for local media (not federation-created ones)
        if (media.PeerServerId is not null)
            return;

        // Get external IDs for the local media (excluding "federation" provider)
        var localExternalIds = media.ExternalIds
            .Where(e => e.ProviderName != "federation")
            .Select(e => new { e.ProviderName, e.Value })
            .ToList();

        if (localExternalIds.Count == 0)
            return;

        // Find federation media with matching ExternalIds
        var providerNames = localExternalIds.Select(e => e.ProviderName).ToList();
        var providerValues = localExternalIds.Select(e => e.Value).ToList();

        var federationMediaIds = await context.Medias
            .Where(m => m.PeerServerId != null && m.Id != media.Id)
            .Where(m => m.ExternalIds.Any(e =>
                providerNames.Contains(e.ProviderName) && providerValues.Contains(e.Value)))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (federationMediaIds.Count == 0)
            return;

        // Verify actual pair match (not just any provider+value combo)
        var matchingMediaIds = new List<Guid>();
        foreach (var candidateId in federationMediaIds)
        {
            var candidateExternalIds = await context.ExternalIds
                .Where(e => e.MediaId == candidateId)
                .Select(e => new { e.ProviderName, e.Value })
                .ToListAsync(cancellationToken);

            var hasMatch = localExternalIds.Any(local =>
                candidateExternalIds.Any(c => c.ProviderName == local.ProviderName && c.Value == local.Value));

            if (hasMatch)
                matchingMediaIds.Add(candidateId);
        }

        if (matchingMediaIds.Count == 0)
            return;

        logger.LogInformation(
            "Re-identifying {Count} federation media to local media {MediaId}",
            matchingMediaIds.Count, media.Id);

        // Reparent RemoteIndexedFiles from federation media to local media
        var remoteFiles = await context.RemoteIndexedFiles
            .Where(f => matchingMediaIds.Contains(f.MediaId))
            .ToListAsync(cancellationToken);

        foreach (var remoteFile in remoteFiles)
        {
            remoteFile.MediaId = media.Id;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reparented {FileCount} remote files from {MediaCount} federation media to local media {MediaId}",
            remoteFiles.Count, matchingMediaIds.Count, media.Id);
    }
}
