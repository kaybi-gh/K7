using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.Diagnostics.Services;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.IndexedFiles.Commands.ExtractChapters;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
using K7.Server.Application.Features.Medias.Commands.ExtractSerieThemeSong;
using K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Diagnostics.Commands.FixDiagnosticItems;

[Authorize(Roles = Roles.Administrator)]
public record FixDiagnosticItemsCommand : IRequest<int>
{
    public required IReadOnlyList<Guid> EntityIds { get; init; }
    public required DiagnosticFixAction Action { get; init; }
}

public class FixDiagnosticItemsCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    OrphanIndexedFileFixBuilder orphanIndexedFileFixBuilder,
    ILogger<FixDiagnosticItemsCommandHandler> logger)
    : IRequestHandler<FixDiagnosticItemsCommand, int>
{
    public async Task<int> Handle(FixDiagnosticItemsCommand request, CancellationToken cancellationToken)
    {
        if (request.Action == DiagnosticFixAction.RetryCreateMedia)
        {
            var items = await orphanIndexedFileFixBuilder.BuildCreateMediaTasksAsync(request.EntityIds, cancellationToken);
            if (items.Count == 0)
                return 0;

            await sender.Send(new CreateBackgroundTasksBatchCommand(items), cancellationToken);
            return request.EntityIds.Count;
        }

        var successCount = 0;

        foreach (var entityId in request.EntityIds)
        {
            try
            {
                switch (request.Action)
                {
                    case DiagnosticFixAction.RefreshMetadata:
                        await sender.Send(new QueueRefreshMediaMetadataCommand { MediaId = entityId }, cancellationToken);
                        break;

                    case DiagnosticFixAction.AutoReidentifyMetadata:
                        await sender.Send(new QueueRefreshMediaMetadataCommand { MediaId = entityId }, cancellationToken);
                        break;

                    case DiagnosticFixAction.ExtractFileMetadata:
                        await QueueExtractFileMetadataAsync(entityId, cancellationToken);
                        break;

                    case DiagnosticFixAction.ComputeHlsSegments:
                        await sender.Send(new CreateBackgroundTaskCommand
                        {
                            Request = new ComputeHlsSegmentsCommand { Id = entityId, SegmentsDuration = TimeSpan.FromMilliseconds(HlsSegmentHelper.TargetSegmentDurationMs) },
                            TargetEntityId = entityId,
                            TargetEntityTypeName = nameof(IndexedFile),
                            Lane = BackgroundTaskLane.FfmpegPrepare,
                            WorkClass = BackgroundTaskWorkClass.Prepare,
                            TriggeredBy = BackgroundTaskTriggeredBy.Diagnostics,
                            MaxAttempts = 1
                        }, cancellationToken);
                        break;

                    case DiagnosticFixAction.ExtractChapters:
                        await sender.Send(new CreateBackgroundTaskCommand
                        {
                            Request = new ExtractChaptersCommand { Id = entityId },
                            TargetEntityId = entityId,
                            TargetEntityTypeName = nameof(IndexedFile),
                            Lane = BackgroundTaskLane.Probe,
                            WorkClass = BackgroundTaskWorkClass.Prepare,
                            TriggeredBy = BackgroundTaskTriggeredBy.Diagnostics,
                            MaxAttempts = 3
                        }, cancellationToken);
                        break;

                    case DiagnosticFixAction.ExtractSerieThemeSong:
                        await QueueExtractSerieThemeSongOrDetectAsync(entityId, cancellationToken);
                        break;

                    case DiagnosticFixAction.DetectMediaSegments:
                        await QueueDetectMediaSegmentsForEpisodeAsync(entityId, cancellationToken);
                        break;

                    case DiagnosticFixAction.AnalyzeMusicTrackAudio:
                        await sender.Send(new CreateBackgroundTaskCommand
                        {
                            Request = new AnalyzeMusicTrackAudioCommand { TrackId = entityId },
                            TargetEntityId = entityId,
                            TargetEntityTypeName = nameof(MusicTrack),
                            Lane = BackgroundTaskLane.MediaAnalysis,
                            WorkClass = BackgroundTaskWorkClass.Polish,
                            TriggeredBy = BackgroundTaskTriggeredBy.Diagnostics,
                            MaxAttempts = 2
                        }, cancellationToken);
                        break;
                }

                successCount++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to apply fix action {Action} on entity {EntityId}", request.Action, entityId);
            }
        }

        return successCount;
    }

    private async Task QueueExtractSerieThemeSongOrDetectAsync(Guid serieId, CancellationToken cancellationToken)
    {
        var hasIntro = await ThemeSongDiagnosticHelper.SerieHasIntroAsync(context, serieId, cancellationToken);
        if (hasIntro)
        {
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new ExtractSerieThemeSongCommand { SerieId = serieId },
                TargetEntityId = serieId,
                TargetEntityTypeName = nameof(Serie),
                Lane = BackgroundTaskLane.MediaAnalysis,
                WorkClass = BackgroundTaskWorkClass.Polish,
                TriggeredBy = BackgroundTaskTriggeredBy.Diagnostics,
                MaxAttempts = 2
            }, cancellationToken);
            return;
        }

        var seasonIds = await ThemeSongDiagnosticHelper.GetEligibleSeasonIdsForSerieAsync(
            context, serieId, cancellationToken);
        foreach (var seasonId in seasonIds)
        {
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new DetectMediaSegmentsCommand { SeasonId = seasonId },
                TargetEntityId = seasonId,
                TargetEntityTypeName = nameof(SerieSeason),
                Lane = BackgroundTaskLane.MediaAnalysis,
                WorkClass = BackgroundTaskWorkClass.Polish,
                TriggeredBy = BackgroundTaskTriggeredBy.Diagnostics,
                MaxAttempts = 2
            }, cancellationToken);
        }
    }

    private async Task QueueDetectMediaSegmentsForEpisodeAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        var seasonId = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => e.Id == episodeId)
            .Select(e => (Guid?)e.SeasonId)
            .FirstOrDefaultAsync(cancellationToken);

        if (seasonId is null)
            return;

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new DetectMediaSegmentsCommand { SeasonId = seasonId.Value },
            TargetEntityId = seasonId.Value,
            TargetEntityTypeName = nameof(SerieSeason),
            Lane = BackgroundTaskLane.MediaAnalysis,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.Diagnostics,
            MaxAttempts = 2
        }, cancellationToken);
    }

    private async Task QueueExtractFileMetadataAsync(Guid indexedFileId, CancellationToken cancellationToken)
    {
        var libraryMediaType = await context.IndexedFiles
            .Where(f => f.Id == indexedFileId)
            .Select(f => context.Libraries
                .Where(l => l.Id == f.LibraryId)
                .Select(l => l.MediaType)
                .FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);

        var fileType = libraryMediaType == LibraryMediaType.Music ? FileType.Audio : FileType.Video;

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new CreateFileMetadatasCommand { Id = indexedFileId, FileType = fileType },
            TargetEntityId = indexedFileId,
            TargetEntityTypeName = nameof(IndexedFile),
            Lane = BackgroundTaskLane.Probe,
            WorkClass = BackgroundTaskWorkClass.CriticalProbe,
            TriggeredBy = BackgroundTaskTriggeredBy.Diagnostics,
            MaxAttempts = 1
        }, cancellationToken);
    }
}
