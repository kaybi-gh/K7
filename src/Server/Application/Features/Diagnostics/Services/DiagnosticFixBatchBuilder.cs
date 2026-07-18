using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.IndexedFiles.Commands.ExtractChapters;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Diagnostics.Services;

public class DiagnosticFixBatchBuilder(IApplicationDbContext context, OrphanIndexedFileFixBuilder orphanIndexedFileFixBuilder)
{
    public async Task<List<CreateBackgroundTasksBatchItem>> BuildBatchItemsAsync(
        DiagnosticFixAction action,
        IReadOnlyList<Guid> entityIds,
        CancellationToken cancellationToken)
    {
        return action switch
        {
            DiagnosticFixAction.RetryCreateMedia => await orphanIndexedFileFixBuilder.BuildCreateMediaTasksAsync(entityIds, cancellationToken),
            DiagnosticFixAction.AnalyzeMusicTrackAudio => entityIds.Select(trackId => new CreateBackgroundTasksBatchItem
            {
                Request = new AnalyzeMusicTrackAudioCommand { TrackId = trackId },
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = trackId,
                TargetEntityTypeName = nameof(MusicTrack),
                MaxAttempts = 2,
                ConcurrencyGroup = "ffmpeg"
            }).ToList(),
            DiagnosticFixAction.ComputeHlsSegments => entityIds.Select(fileId => new CreateBackgroundTasksBatchItem
            {
                Request = new ComputeHlsSegmentsCommand { Id = fileId, SegmentsDuration = TimeSpan.FromSeconds(2) },
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = fileId,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 1,
                ConcurrencyGroup = "hls-segments"
            }).ToList(),
            DiagnosticFixAction.ExtractChapters => entityIds.Select(fileId => new CreateBackgroundTasksBatchItem
            {
                Request = new ExtractChaptersCommand { Id = fileId },
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = fileId,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 3,
                ConcurrencyGroup = "ffprobe"
            }).ToList(),
            DiagnosticFixAction.ExtractFileMetadata => await BuildExtractFileMetadataItemsAsync(entityIds, cancellationToken),
            _ => []
        };
    }

    public static bool UsesBackgroundTaskBatch(DiagnosticFixAction action) =>
        action is DiagnosticFixAction.AnalyzeMusicTrackAudio
            or DiagnosticFixAction.ComputeHlsSegments
            or DiagnosticFixAction.ExtractChapters
            or DiagnosticFixAction.ExtractFileMetadata
            or DiagnosticFixAction.RetryCreateMedia;

    private async Task<List<CreateBackgroundTasksBatchItem>> BuildExtractFileMetadataItemsAsync(
        IReadOnlyList<Guid> indexedFileIds,
        CancellationToken cancellationToken)
    {
        var files = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => indexedFileIds.Contains(f.Id))
            .Select(f => new
            {
                f.Id,
                LibraryMediaType = context.Libraries
                    .Where(l => l.Id == f.LibraryId)
                    .Select(l => l.MediaType)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return files.Select(f => new CreateBackgroundTasksBatchItem
        {
            Request = new CreateFileMetadatasCommand
            {
                Id = f.Id,
                FileType = f.LibraryMediaType == LibraryMediaType.Music ? FileType.Audio : FileType.Video
            },
            Priority = BackgroundTaskPriority.Normal,
            TargetEntityId = f.Id,
            TargetEntityTypeName = nameof(IndexedFile),
            MaxAttempts = 1,
            ConcurrencyGroup = "file-metadata"
        }).ToList();
    }
}
