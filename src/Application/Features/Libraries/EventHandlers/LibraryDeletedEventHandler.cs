using MediaServer.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.Libraries.EventHandlers;

public class LibraryDeletedEventHandler : INotificationHandler<LibraryDeletedEvent>
{
    private readonly ILogger<LibraryDeletedEventHandler> _logger;

    public LibraryDeletedEventHandler(ILogger<LibraryDeletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(LibraryDeletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
