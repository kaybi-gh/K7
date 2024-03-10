using MediaServer.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.IndexedFiles.EventHandlers;

public class IndexedFileDeletedEventHandler : INotificationHandler<IndexedFileDeletedEvent>
{
    private readonly ILogger<IndexedFileDeletedEvent> _logger;

    public IndexedFileDeletedEventHandler(ILogger<IndexedFileDeletedEvent> logger)
    {
        _logger = logger;
    }

    public Task Handle(IndexedFileDeletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
