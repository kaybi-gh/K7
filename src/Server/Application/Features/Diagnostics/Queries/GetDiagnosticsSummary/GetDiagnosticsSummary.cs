using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Diagnostics;

namespace K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticsSummary;

[Authorize(Roles = Roles.Administrator)]
public record GetDiagnosticsSummaryQuery : IRequest<List<LibraryHealthSummaryDto>>;

public class GetDiagnosticsSummaryQueryHandler : IRequestHandler<GetDiagnosticsSummaryQuery, List<LibraryHealthSummaryDto>>
{
    private static readonly BackgroundTaskStatus[] PendingBackgroundTaskStatuses =
    [
        BackgroundTaskStatus.Pending,
        BackgroundTaskStatus.InProgress,
        BackgroundTaskStatus.WaitingForRetry
    ];

    private readonly IApplicationDbContext _context;

    public GetDiagnosticsSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LibraryHealthSummaryDto>> Handle(GetDiagnosticsSummaryQuery request, CancellationToken cancellationToken)
    {
        var libraries = await _context.Libraries
            .AsNoTracking()
            .Select(l => new LibrarySnapshot(
                l.Id,
                l.Title,
                l.MediaType,
                l.MetadataRefreshIntervalDays))
            .ToListAsync(cancellationToken);

        if (libraries.Count == 0)
            return [];

        var indexedFileStats = await GetIndexedFileStatsAsync(cancellationToken);
        var missingHlsSegmentCounts = await GetMissingHlsSegmentCountsAsync(cancellationToken);
        var inaccessiblePathCounts = await GetInaccessiblePathCountsAsync(cancellationToken);
        var mediaWithoutFilesCounts = await GetMediaWithoutFilesCountsAsync(cancellationToken);

        var musicLibraryIds = libraries
            .Where(l => l.MediaType == LibraryMediaType.Music)
            .Select(l => l.Id)
            .ToList();
        var missingAudioAnalysisCounts = await GetMissingAudioAnalysisCountsAsync(musicLibraryIds, cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;
        var result = new List<LibraryHealthSummaryDto>(libraries.Count);

        foreach (var library in libraries)
        {
            var staleMetadataThreshold = MetadataStalenessHelper.GetStalenessThresholdUtc(
                library.MetadataRefreshIntervalDays, utcNow);

            var linkedMediaStats = await GetLinkedMediaStatsAsync(
                library.Id, staleMetadataThreshold, cancellationToken);
            var backgroundTaskStats = await GetBackgroundTaskStatsAsync(library.Id, cancellationToken);

            indexedFileStats.TryGetValue(library.Id, out var fileStats);
            missingHlsSegmentCounts.TryGetValue(library.Id, out var missingHlsSegmentsCount);
            inaccessiblePathCounts.TryGetValue(library.Id, out var inaccessiblePathCount);
            mediaWithoutFilesCounts.TryGetValue(library.Id, out var mediaWithoutFilesCount);
            missingAudioAnalysisCounts.TryGetValue(library.Id, out var missingAudioAnalysisCount);

            result.Add(new LibraryHealthSummaryDto
            {
                LibraryId = library.Id,
                LibraryTitle = library.Title,
                MediaType = library.MediaType,
                TotalMediaCount = linkedMediaStats.TotalMediaCount,
                MediaMissingPicturesCount = linkedMediaStats.MediaMissingPicturesCount,
                MediaMissingExternalIdCount = linkedMediaStats.MediaMissingExternalIdCount,
                MediaMissingMetadataCount = linkedMediaStats.MediaMissingMetadataCount,
                MediaWithoutFilesCount = mediaWithoutFilesCount,
                StaleMetadataCount = linkedMediaStats.StaleMetadataCount,
                TotalIndexedFileCount = fileStats?.TotalCount ?? 0,
                OrphanIndexedFileCount = fileStats?.OrphanCount ?? 0,
                UnidentifiedIndexedFileCount = fileStats?.UnidentifiedCount ?? 0,
                MissingFileMetadataCount = fileStats?.MissingFileMetadataCount ?? 0,
                MissingHlsSegmentsCount = missingHlsSegmentsCount,
                MissingAudioAnalysisCount = missingAudioAnalysisCount,
                InaccessiblePathCount = inaccessiblePathCount,
                PendingBackgroundTaskCount = backgroundTaskStats.PendingCount,
                FailedBackgroundTaskCount = backgroundTaskStats.FailedCount
            });
        }

        return result;
    }

    private async Task<Dictionary<Guid, IndexedFileLibraryStats>> GetIndexedFileStatsAsync(CancellationToken cancellationToken)
    {
        var stats = await _context.IndexedFiles
            .AsNoTracking()
            .GroupBy(f => f.LibraryId)
            .Select(g => new IndexedFileLibraryStats(
                g.Key,
                g.Count(),
                g.Count(f => f.MediaId == null),
                g.Count(f => f.Identification == null),
                g.Count(f => f.FileMetadata == null)))
            .ToListAsync(cancellationToken);

        return stats.ToDictionary(s => s.LibraryId);
    }

    private async Task<Dictionary<Guid, int>> GetMissingHlsSegmentCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.FileMetadata != null && f.FileMetadata.Type == FileType.Video)
            .Where(f => _context.Libraries.Any(l => l.Id == f.LibraryId && l.TransmuxingEnabled))
            .Where(f => !_context.HlsSegments.Any(s => s.IndexedFileId == f.Id))
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> GetInaccessiblePathCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.ScanIssues
            .AsNoTracking()
            .GroupBy(s => s.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> GetMediaWithoutFilesCountsAsync(CancellationToken cancellationToken)
    {
        var libraryMediaRefs = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId != null)
            .Select(f => new { f.LibraryId, MediaId = f.MediaId!.Value })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (libraryMediaRefs.Count == 0)
            return [];

        var referencedMediaIds = libraryMediaRefs
            .Select(x => x.MediaId)
            .Distinct()
            .ToList();

        var mediaIdsWithoutFiles = await _context.Medias
            .AsNoTracking()
            .Where(m => referencedMediaIds.Contains(m.Id))
            .Where(m => !m.IndexedFiles.Any())
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (mediaIdsWithoutFiles.Count == 0)
            return [];

        var mediaWithoutFilesSet = mediaIdsWithoutFiles.ToHashSet();

        return libraryMediaRefs
            .Where(x => mediaWithoutFilesSet.Contains(x.MediaId))
            .GroupBy(x => x.LibraryId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.MediaId).Distinct().Count());
    }

    private async Task<Dictionary<Guid, int>> GetMissingAudioAnalysisCountsAsync(
        IReadOnlyCollection<Guid> musicLibraryIds,
        CancellationToken cancellationToken)
    {
        if (musicLibraryIds.Count == 0)
            return [];

        var counts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => musicLibraryIds.Contains(f.LibraryId) && f.MediaId != null)
            .Join(
                _context.Medias.OfType<MusicTrack>().Where(t => t.AudioAnalysis == null),
                f => f.MediaId,
                t => t.Id,
                (f, t) => new { f.LibraryId, TrackId = t.Id })
            .Distinct()
            .GroupBy(x => x.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<LinkedMediaLibraryStats> GetLinkedMediaStatsAsync(
        Guid libraryId,
        DateTimeOffset? staleMetadataThreshold,
        CancellationToken cancellationToken)
    {
        var linkedMedia = _context.Medias.AsNoTracking().WhereLinkedToLibrary(libraryId);

        var stats = await linkedMedia
            .GroupBy(_ => 1)
            .Select(g => new LinkedMediaLibraryStats(
                g.Count(),
                g.Count(m => !m.Pictures.Any()),
                g.Count(m => (m is Movie || m is Serie || m is MusicAlbum) && !m.ExternalIds.Any()),
                g.Count(m => m.ExternalIds.Any() && !m.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)),
                staleMetadataThreshold == null
                    ? 0
                    : g.Count(m => (m is Movie || m is MusicAlbum || m is Serie || m is MusicArtist)
                        && (m.LastMetadataRefreshedAt == null || m.LastMetadataRefreshedAt < staleMetadataThreshold))))
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new LinkedMediaLibraryStats(0, 0, 0, 0, 0);
    }

    private async Task<BackgroundTaskLibraryStats> GetBackgroundTaskStatsAsync(
        Guid libraryId,
        CancellationToken cancellationToken)
    {
        var linkedMediaIds = _context.Medias.WhereLinkedToLibrary(libraryId).Select(m => m.Id);

        var stats = await _context.BackgroundTasks
            .AsNoTracking()
            .Where(t => t.TargetEntityId != null && linkedMediaIds.Contains(t.TargetEntityId.Value))
            .GroupBy(_ => 1)
            .Select(g => new BackgroundTaskLibraryStats(
                g.Count(t => PendingBackgroundTaskStatuses.Contains(t.Status)),
                g.Count(t => t.Status == BackgroundTaskStatus.Failed)))
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new BackgroundTaskLibraryStats(0, 0);
    }

    private sealed record LibrarySnapshot(
        Guid Id,
        string Title,
        LibraryMediaType MediaType,
        int? MetadataRefreshIntervalDays);

    private sealed record IndexedFileLibraryStats(
        Guid LibraryId,
        int TotalCount,
        int OrphanCount,
        int UnidentifiedCount,
        int MissingFileMetadataCount);

    private sealed record LinkedMediaLibraryStats(
        int TotalMediaCount,
        int MediaMissingPicturesCount,
        int MediaMissingExternalIdCount,
        int MediaMissingMetadataCount,
        int StaleMetadataCount);

    private sealed record BackgroundTaskLibraryStats(int PendingCount, int FailedCount);
}
