using MediaServer.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.IndexedFiles.EventHandlers;

public class IndexedFileCreatedEventHandler : INotificationHandler<IndexedFileCreatedEvent>
{
    private readonly ILogger<IndexedFileCreatedEventHandler> _logger;

    public IndexedFileCreatedEventHandler(ILogger<IndexedFileCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(IndexedFileCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
