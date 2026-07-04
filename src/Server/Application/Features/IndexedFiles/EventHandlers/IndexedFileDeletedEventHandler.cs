using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.EventHandlers;

public class IndexedFileDeletedEventHandler(
    IMediaQueryCacheInvalidator cacheInvalidator,
    ILogger<IndexedFileDeletedEventHandler> logger) : INotificationHandler<IndexedFileDeletedEvent>
{
    public Task Handle(IndexedFileDeletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);
        cacheInvalidator.InvalidateAll();
        return Task.CompletedTask;
    }
}
