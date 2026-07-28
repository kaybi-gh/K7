using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Commands.GenerateThumbnails;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.EventHandlers;

public class FileMetadataCreatedEventHandler(
    ILogger<FileMetadataCreatedEventHandler> logger,
    ISender sender,
    IApplicationDbContext context) : INotificationHandler<FileMetadataCreatedEvent>
{
    public async Task Handle(FileMetadataCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);

        var library = await context.Libraries
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == notification.IndexedFile.LibraryId, cancellationToken);

        if (library is null)
            return;

        if (library.TransmuxingEnabled)
        {
            await sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new ComputeHlsSegmentsCommand()
                {
                    Id = notification.IndexedFile.Id,
                    SegmentsDuration = TimeSpan.FromMilliseconds(HlsSegmentHelper.TargetSegmentDurationMs)
                },
                TargetEntityId = notification.IndexedFile.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                Lane = BackgroundTaskLane.FfmpegPrepare,
                WorkClass = BackgroundTaskWorkClass.Prepare,
                TriggeredBy = BackgroundTaskTriggeredBy.System,
                MaxAttempts = 5
            }, cancellationToken);
        }

        if (notification.FileType == FileType.Video && library.SeekbarThumbnailGenerationEnabled)
        {
            await sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new GenerateThumbnailsCommand()
                {
                    Id = notification.IndexedFile.Id
                },
                TargetEntityId = notification.IndexedFile.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                Lane = BackgroundTaskLane.ImageExtract,
                WorkClass = BackgroundTaskWorkClass.Polish,
                TriggeredBy = BackgroundTaskTriggeredBy.System,
                MaxAttempts = 1
            }, cancellationToken);
        }

        if (notification.FileType == FileType.Video && library.IntroDetectionEnabled)
        {
            await TriggerIntroDetectionIfEligibleAsync(notification.IndexedFile, cancellationToken);
        }
    }

    private async Task TriggerIntroDetectionIfEligibleAsync(IndexedFile indexedFile, CancellationToken cancellationToken)
    {
        // The media may not exist yet: probes are enqueued during the scan while media creation runs
        // afterwards. MediaCreatedIntroDetectionEventHandler covers that ordering.
        if (indexedFile.MediaId is null)
        {
            logger.LogDebug("Intro detection deferred to media creation: file {FileId} has no MediaId", indexedFile.Id);
            return;
        }

        await IntroDetectionQueueHelper.TryQueueForEpisodeAsync(context, sender, indexedFile.MediaId.Value, logger, cancellationToken);
    }
}
