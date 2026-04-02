using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.EventHandlers;

public class IndexedFileCreatedEventHandler : INotificationHandler<IndexedFileCreatedEvent>
{
    private readonly ILogger<IndexedFileCreatedEventHandler> _logger;
    private readonly ISender _sender;

    public IndexedFileCreatedEventHandler(ILogger<IndexedFileCreatedEventHandler> logger, ISender sender)
    {
        _logger = logger;
        _sender = sender;
    }

    public async Task Handle(IndexedFileCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);

        // TODO - Create subtasks
        await _sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new CreateFileMetadatasCommand()
            {
                Id = notification.IndexedFile.Id,
                FileType = notification.FileType
            },
            Priority = BackgroundTaskPriority.VeryHigh,
            TargetEntityId = notification.IndexedFile.Id,
            TargetEntityTypeName = nameof(IndexedFile),
            MaxAttempts = 5,
            ConcurrencyGroup = "ffmpeg"
        }, cancellationToken);
    }
}
