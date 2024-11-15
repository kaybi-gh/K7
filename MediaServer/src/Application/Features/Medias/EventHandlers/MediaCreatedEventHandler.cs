using MediaServer.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.Medias.EventHandlers;

public class MediaCreatedEventHandler : INotificationHandler<MediaCreatedEvent>
{
    private readonly ILogger<MediaCreatedEventHandler> _logger;

    public MediaCreatedEventHandler(ILogger<MediaCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
