using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class ContinueWatchingNewEpisodeEventHandler(
    INextEpisodeEnqueueService nextEpisodeEnqueueService,
    IApplicationDbContext context,
    ILogger<ContinueWatchingNewEpisodeEventHandler> logger)
    : INotificationHandler<MediaCreatedEvent>,
      INotificationHandler<SerieEpisodeBecamePlayableEvent>
{
    public Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Media is not SerieEpisode episode)
            return Task.CompletedTask;

        return EnqueueAndSaveAsync(episode.Id, cancellationToken);
    }

    public Task Handle(SerieEpisodeBecamePlayableEvent notification, CancellationToken cancellationToken) =>
        EnqueueAndSaveAsync(notification.EpisodeId, cancellationToken);

    private async Task EnqueueAndSaveAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        await nextEpisodeEnqueueService.EnqueueWatchersForNewEpisodeAsync(episodeId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogDebug("Keep Watching catch-up evaluated for new episode {EpisodeId}", episodeId);
    }
}
