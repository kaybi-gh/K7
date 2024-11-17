using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.EventHandlers;

public class MediaCreatedEventHandler : INotificationHandler<MediaCreatedEvent>
{
    private readonly ILogger<MediaCreatedEventHandler> _logger;

    public MediaCreatedEventHandler(ILogger<MediaCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(MediaCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
