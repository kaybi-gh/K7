using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class MediaCreatedNotificationHandler(ILibraryNotifier notifier, ILogger<MediaCreatedNotificationHandler> logger)
    : INotificationHandler<MediaCreatedEvent>
{
    public async Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        var media = notification.Media;

        logger.LogDebug("Broadcasting MediaAdded for {MediaId} '{Title}'", media.Id, media.Title);

        await notifier.NotifyMediaAddedAsync(
            media.Id,
            media.Title,
            media.Type.ToString(),
            cancellationToken);
    }
}
