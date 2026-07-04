using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Libraries.EventHandlers;

public class LibraryCreatedEventHandler : INotificationHandler<LibraryCreatedEvent>
{
    private readonly ILogger<LibraryCreatedEventHandler> _logger;

    public LibraryCreatedEventHandler(ILogger<LibraryCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(LibraryCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
