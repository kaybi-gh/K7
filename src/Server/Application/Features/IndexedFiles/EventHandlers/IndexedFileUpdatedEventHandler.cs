using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.EventHandlers;

public class IndexedFileUpdatedEventHandler : INotificationHandler<IndexedFileUpdatedEvent>
{
    private readonly ILogger<IndexedFileUpdatedEventHandler> _logger;

    public IndexedFileUpdatedEventHandler(ILogger<IndexedFileUpdatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(IndexedFileUpdatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
