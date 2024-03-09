using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Services;
using MediaServer.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.Medias.EventHandlers;

public class MediaCreatedEventHandler : INotificationHandler<MovieCreatedEvent>
{
    private readonly ILogger<MediaCreatedEventHandler> _logger;

    public MediaCreatedEventHandler(ILogger<MediaCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(MovieCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
