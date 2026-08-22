using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.EventHandlers;

public class IndexedFileDeletedEventHandler(
    IApplicationDbContext context,
    IMusicIntelligenceCatalogReconciler musicIntelligenceCatalogReconciler,
    IMediaQueryCacheInvalidator cacheInvalidator,
    ILogger<IndexedFileDeletedEventHandler> logger) : INotificationHandler<IndexedFileDeletedEvent>
{
    public async Task Handle(IndexedFileDeletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        cacheInvalidator.InvalidateAll();

        if (notification.FormerMediaId is not Guid formerMediaId)
            return;

        if (await context.Medias
            .OfType<MusicTrack>()
            .AnyAsync(t => t.Id == formerMediaId, cancellationToken))
        {
            var deleted = await MusicOrphanCleanupHelper.TryDeleteTrackIfOrphanAsync(
                context,
                formerMediaId,
                logger,
                cancellationToken);

            if (!deleted)
                return;

            await context.SaveChangesAsync(cancellationToken);
            musicIntelligenceCatalogReconciler.RequestReconcile();
            return;
        }

        if (!await context.Medias
            .OfType<SerieEpisode>()
            .AnyAsync(e => e.Id == formerMediaId, cancellationToken))
            return;

        var episodeDeleted = await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
            context,
            formerMediaId,
            logger,
            cancellationToken);

        if (!episodeDeleted)
            return;

        await context.SaveChangesAsync(cancellationToken);
    }
}
