using MediaServer.Domain.Events;
using MediaServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.Libraries.EventHandlers;

public class LibraryCreatedEventHandler : INotificationHandler<LibraryCreatedEvent>
{
    private readonly ILogger<LibraryCreatedEventHandler> _logger;

    public LibraryCreatedEventHandler(ILogger<LibraryCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(LibraryCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
