using MediaServer.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.IndexedFiles.EventHandlers;

public class IndexedFileUpdatedEventHandler : INotificationHandler<IndexedFileUpdatedEvent>
{
    private readonly ILogger<IndexedFileUpdatedEventHandler> _logger;

    public IndexedFileUpdatedEventHandler(ILogger<IndexedFileUpdatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(IndexedFileUpdatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
