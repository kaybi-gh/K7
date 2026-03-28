using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.IndexedFiles.Commands.GenerateThumbnails;
using K7.Server.Domain.Constants;
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
            TargetEntityTypeName = nameof(IndexedFile),
            MaxAttempts = 5,
            ConcurrencyGroup = ConcurrencyGroups.Ffmpeg
        }, cancellationToken);

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
            MaxAttempts = 5,
            ConcurrencyGroup = ConcurrencyGroups.Ffmpeg
        }, cancellationToken);

        if (notification.FileType == FileType.Video)
        {
            await _sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new GenerateThumbnailsCommand()
                {
                    Id = notification.IndexedFile.Id
                },
                Priority = BackgroundTaskPriority.Lowest,
                TargetEntityId = notification.IndexedFile.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 1,
                ConcurrencyGroup = ConcurrencyGroups.Ffmpeg
            }, cancellationToken);
        }
    }
}
