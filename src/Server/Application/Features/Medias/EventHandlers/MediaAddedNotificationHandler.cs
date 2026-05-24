using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class MediaAddedNotificationHandler(
    ILibraryNotifier notifier,
    IMediaQueryCacheInvalidator cacheInvalidator,
    ILogger<MediaAddedNotificationHandler> logger)
    : INotificationHandler<MediaAddedEvent>
{
    public async Task Handle(MediaAddedEvent notification, CancellationToken cancellationToken)
    {
        var media = notification.Media;

        cacheInvalidator.InvalidateAll();

        logger.LogDebug("Broadcasting MediaAdded (with metadata) for {MediaId} '{Title}'", media.Id, media.Title);

        await notifier.NotifyMediaAddedAsync(
            media.Id,
            media.Title,
            media.Type.ToString(),
            cancellationToken);
    }
}
