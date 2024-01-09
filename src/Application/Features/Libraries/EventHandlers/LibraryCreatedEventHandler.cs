using MediaServer.Domain.Events;
using MediaServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.Libraries.EventHandlers;

public class LibraryCreatedEventHandler : INotificationHandler<LibraryCreatedEvent>
{
    private readonly ILogger<LibraryCreatedEventHandler> _logger;
    private readonly IFileIndexerService _fileIndexerService;

    public LibraryCreatedEventHandler(ILogger<LibraryCreatedEventHandler> logger, IFileIndexerService fileIndexerService)
    {
        _logger = logger;
        _fileIndexerService = fileIndexerService;
    }

    public Task Handle(LibraryCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);
        return _fileIndexerService.IndexAsync(notification.Library, cancellationToken);
    }
}
