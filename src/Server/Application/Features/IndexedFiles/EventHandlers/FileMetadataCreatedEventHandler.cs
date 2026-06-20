using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Commands.GenerateThumbnails;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
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
                    SegmentsDuration = TimeSpan.FromSeconds(2)
                },
                Priority = BackgroundTaskPriority.High,
                TargetEntityId = notification.IndexedFile.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 5,
                ConcurrencyGroup = "ffmpeg"
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
                Priority = BackgroundTaskPriority.Lowest,
                TargetEntityId = notification.IndexedFile.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 1,
                ConcurrencyGroup = "ffmpeg"
            }, cancellationToken);
        }

        if (notification.FileType == FileType.Video && library.IntroDetectionEnabled)
        {
            await TriggerIntroDetectionIfEligibleAsync(notification.IndexedFile, cancellationToken);
        }
    }

    private async Task TriggerIntroDetectionIfEligibleAsync(IndexedFile indexedFile, CancellationToken cancellationToken)
    {
        if (indexedFile.MediaId is null)
        {
            logger.LogDebug("Intro detection skipped: file {FileId} has no MediaId", indexedFile.Id);
            return;
        }

        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .FirstOrDefaultAsync(e => e.Id == indexedFile.MediaId, cancellationToken);

        if (episode is null)
        {
            logger.LogDebug("Intro detection skipped: media {MediaId} is not a SerieEpisode", indexedFile.MediaId);
            return;
        }

        var episodeCount = await context.Medias
            .OfType<SerieEpisode>()
            .CountAsync(e => e.SeasonId == episode.SeasonId, cancellationToken);

        if (episodeCount < 2)
        {
            logger.LogDebug("Intro detection skipped: season {SeasonId} has only {Count} episode(s)", episode.SeasonId, episodeCount);
            return;
        }

        logger.LogInformation("Queuing intro detection for season {SeasonId} ({EpisodeCount} episodes)", episode.SeasonId, episodeCount);

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new DetectMediaSegmentsCommand { SeasonId = episode.SeasonId },
            Priority = BackgroundTaskPriority.Low,
            TargetEntityId = episode.SeasonId,
            TargetEntityTypeName = nameof(SerieSeason),
            MaxAttempts = 2,
            ConcurrencyGroup = "ffmpeg"
        }, cancellationToken);
    }
}
