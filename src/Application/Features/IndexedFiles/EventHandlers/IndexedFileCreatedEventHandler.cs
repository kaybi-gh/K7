using MediaServer.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using MediaServer.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using MediaServer.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using MediaServer.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas.Files;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.IndexedFiles.EventHandlers;

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
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);

        // TODO - Create subtasks
        await _sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new CreateFileMetadatasCommand()
            {
                Id = notification.IndexedFile.Id,
                FileType = notification.FileType
            },
            Priority = BackgroundTaskPriority.VeryHigh,
            TargetEntityTypeName = nameof(IndexedFile),
            MaxRetryCount = 5
        }, cancellationToken);

        if (notification.FileType == FileType.Video)
        {
            await _sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new ComputeHlsSegmentsCommand()
                {
                    Id = notification.IndexedFile.Id,
                    SegmentsDuration = TimeSpan.FromSeconds(2)
                },
                Priority = BackgroundTaskPriority.High,
                TargetEntityId = notification.IndexedFile.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxRetryCount = 5
            }, cancellationToken);
        }
    }
}
