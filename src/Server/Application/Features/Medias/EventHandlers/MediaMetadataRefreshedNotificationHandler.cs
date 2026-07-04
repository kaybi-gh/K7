using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class MediaMetadataRefreshedNotificationHandler(
    ILibraryNotifier notifier,
    IMediaQueryCacheInvalidator cacheInvalidator,
    ILogger<MediaMetadataRefreshedNotificationHandler> logger)
    : INotificationHandler<MediaMetadataRefreshedEvent>
{
    public async Task Handle(MediaMetadataRefreshedEvent notification, CancellationToken cancellationToken)
    {
        var media = notification.Media;

        cacheInvalidator.InvalidateAll();

        logger.LogDebug("Broadcasting MediaMetadataRefreshed for {MediaId} '{Title}'", media.Id, media.Title);

        await notifier.NotifyMediaMetadataRefreshedAsync(media.Id, cancellationToken);
    }
}
