using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.EventHandlers;

public class IndexedFileDeletedEventHandler : INotificationHandler<IndexedFileDeletedEvent>
{
    private readonly ILogger<IndexedFileDeletedEvent> _logger;

    public IndexedFileDeletedEventHandler(ILogger<IndexedFileDeletedEvent> logger)
    {
        _logger = logger;
    }

    public Task Handle(IndexedFileDeletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
