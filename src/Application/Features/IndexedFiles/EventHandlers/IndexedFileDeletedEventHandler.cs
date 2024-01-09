using MediaServer.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.Libraries.EventHandlers;

public class IndexedFileDeletedEventHandler : INotificationHandler<LibraryCreatedEvent>
{
    private readonly ILogger<IndexedFileCreatedEventHandler> _logger;

    public IndexedFileDeletedEventHandler(ILogger<IndexedFileCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(LibraryCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
