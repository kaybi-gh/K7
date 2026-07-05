using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.Diagnostics.Services;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;
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
                            Request = new ComputeHlsSegmentsCommand { Id = entityId, SegmentsDuration = TimeSpan.FromSeconds(2) },
                            Priority = BackgroundTaskPriority.Normal,
                            TargetEntityId = entityId,
                            TargetEntityTypeName = nameof(IndexedFile),
                            MaxAttempts = 1,
                            ConcurrencyGroup = "hls-segments"
                        }, cancellationToken);
                        break;

                    case DiagnosticFixAction.AnalyzeMusicTrackAudio:
                        await sender.Send(new CreateBackgroundTaskCommand
                        {
                            Request = new AnalyzeMusicTrackAudioCommand { TrackId = entityId },
                            Priority = BackgroundTaskPriority.Low,
                            TargetEntityId = entityId,
                            TargetEntityTypeName = nameof(MusicTrack),
                            MaxAttempts = 2,
                            ConcurrencyGroup = "ffmpeg"
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
            Priority = BackgroundTaskPriority.Normal,
            TargetEntityId = indexedFileId,
            TargetEntityTypeName = nameof(IndexedFile),
            MaxAttempts = 1,
            ConcurrencyGroup = "file-metadata"
        }, cancellationToken);
    }
}
