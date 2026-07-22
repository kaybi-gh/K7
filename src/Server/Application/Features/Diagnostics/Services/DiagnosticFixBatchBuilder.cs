using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.IndexedFiles.Commands.ExtractChapters;
using K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
using K7.Server.Application.Features.Medias.Commands.ExtractSerieThemeSong;
using K7.Server.Application.Helpers;
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
            DiagnosticFixAction.ExtractSerieThemeSong => await BuildExtractSerieThemeSongItemsAsync(entityIds, cancellationToken),
            DiagnosticFixAction.DetectMediaSegments => await BuildDetectMediaSegmentsItemsFromEpisodesAsync(entityIds, cancellationToken),
            DiagnosticFixAction.ExtractFileMetadata => await BuildExtractFileMetadataItemsAsync(entityIds, cancellationToken),
            _ => []
        };
    }

    public static bool UsesBackgroundTaskBatch(DiagnosticFixAction action) =>
        action is DiagnosticFixAction.AnalyzeMusicTrackAudio
            or DiagnosticFixAction.ComputeHlsSegments
            or DiagnosticFixAction.ExtractChapters
            or DiagnosticFixAction.ExtractSerieThemeSong
            or DiagnosticFixAction.DetectMediaSegments
            or DiagnosticFixAction.ExtractFileMetadata
            or DiagnosticFixAction.RetryCreateMedia;

    private async Task<List<CreateBackgroundTasksBatchItem>> BuildExtractSerieThemeSongItemsAsync(
        IReadOnlyList<Guid> serieIds,
        CancellationToken cancellationToken)
    {
        var items = new List<CreateBackgroundTasksBatchItem>();
        var queuedSeasonIds = new HashSet<Guid>();

        foreach (var serieId in serieIds)
        {
            var hasIntro = await ThemeSongDiagnosticHelper.SerieHasIntroAsync(context, serieId, cancellationToken);
            if (hasIntro)
            {
                items.Add(new CreateBackgroundTasksBatchItem
                {
                    Request = new ExtractSerieThemeSongCommand { SerieId = serieId },
                    Priority = BackgroundTaskPriority.Lowest,
                    TargetEntityId = serieId,
                    TargetEntityTypeName = nameof(Serie),
                    MaxAttempts = 2,
                    ConcurrencyGroup = "ffmpeg"
                });
                continue;
            }

            var seasonIds = await ThemeSongDiagnosticHelper.GetEligibleSeasonIdsForSerieAsync(
                context, serieId, cancellationToken);
            foreach (var seasonId in seasonIds)
            {
                if (!queuedSeasonIds.Add(seasonId))
                    continue;

                items.Add(CreateDetectMediaSegmentsItem(seasonId));
            }
        }

        return items;
    }

    private async Task<List<CreateBackgroundTasksBatchItem>> BuildDetectMediaSegmentsItemsFromEpisodesAsync(
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken)
    {
        var seasonIds = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => episodeIds.Contains(e.Id))
            .Select(e => e.SeasonId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return seasonIds.Select(CreateDetectMediaSegmentsItem).ToList();
    }

    private static CreateBackgroundTasksBatchItem CreateDetectMediaSegmentsItem(Guid seasonId) =>
        new()
        {
            Request = new DetectMediaSegmentsCommand { SeasonId = seasonId },
            Priority = BackgroundTaskPriority.Low,
            TargetEntityId = seasonId,
            TargetEntityTypeName = nameof(SerieSeason),
            MaxAttempts = 2,
            ConcurrencyGroup = "ffmpeg"
        };

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
