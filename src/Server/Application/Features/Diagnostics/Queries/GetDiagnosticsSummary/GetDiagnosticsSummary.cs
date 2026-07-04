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
    private readonly IApplicationDbContext _context;

    public GetDiagnosticsSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LibraryHealthSummaryDto>> Handle(GetDiagnosticsSummaryQuery request, CancellationToken cancellationToken)
    {
        var libraries = await _context.Libraries
            .AsNoTracking()
            .Select(l => new
            {
                l.Id,
                l.Title,
                l.MediaType,
                l.MetadataRefreshIntervalDays
            })
            .ToListAsync(cancellationToken);

        var result = new List<LibraryHealthSummaryDto>();

        foreach (var library in libraries)
        {
            var libraryFileMediaIds = _context.IndexedFiles
                .Where(f => f.LibraryId == library.Id && f.MediaId != null)
                .Select(f => f.MediaId!.Value)
                .Distinct();

            var linkedMedia = _context.Medias.WhereLinkedToLibrary(library.Id);
            var linkedMediaIds = linkedMedia.Select(m => m.Id);
            var refreshableLinkedMedia = linkedMedia
                .Where(m => m is Movie || m is MusicAlbum || m is Serie || m is MusicArtist);

            var totalMediaCount = await linkedMedia.CountAsync(cancellationToken);

            var mediaMissingPicturesCount = await linkedMedia
                .Where(m => !m.Pictures.Any())
                .CountAsync(cancellationToken);

            var mediaMissingExternalIdCount = await refreshableLinkedMedia
                .Where(m => m is Movie || m is Serie || m is MusicAlbum)
                .Where(m => !m.ExternalIds.Any())
                .CountAsync(cancellationToken);

            var mediaMissingMetadataCount = await linkedMedia
                .Where(m => m.ExternalIds.Any())
                .Where(m => !m.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre))
                .CountAsync(cancellationToken);

            var mediaWithoutFilesCount = await _context.Medias
                .Where(m => libraryFileMediaIds.Contains(m.Id))
                .Where(m => !m.IndexedFiles.Any())
                .CountAsync(cancellationToken);

            var staleMetadataThreshold = MetadataStalenessHelper.GetStalenessThresholdUtc(
                library.MetadataRefreshIntervalDays, DateTimeOffset.UtcNow);
            var staleMetadataCount = staleMetadataThreshold is null
                ? 0
                : await refreshableLinkedMedia
                    .Where(m => m.LastMetadataRefreshedAt == null || m.LastMetadataRefreshedAt < staleMetadataThreshold)
                    .CountAsync(cancellationToken);

            var totalIndexedFileCount = await _context.IndexedFiles
                .Where(f => f.LibraryId == library.Id)
                .CountAsync(cancellationToken);

            var orphanCount = await _context.IndexedFiles
                .Where(f => f.LibraryId == library.Id && f.MediaId == null)
                .CountAsync(cancellationToken);

            var unidentifiedCount = await _context.IndexedFiles
                .Where(f => f.LibraryId == library.Id && f.Identification == null)
                .CountAsync(cancellationToken);

            var missingFileMetadataCount = await _context.IndexedFiles
                .Where(f => f.LibraryId == library.Id && f.FileMetadata == null)
                .CountAsync(cancellationToken);

            var missingHlsSegmentsCount = await _context.IndexedFiles
                .Where(f => f.LibraryId == library.Id && f.FileMetadata != null
                    && !_context.HlsSegments.Any(s => s.FileMetadataId == f.FileMetadata!.Id))
                .CountAsync(cancellationToken);

            var missingAudioAnalysisCount = library.MediaType == LibraryMediaType.Music
                ? await _context.Medias.OfType<MusicTrack>()
                    .Where(t => libraryFileMediaIds.Contains(t.Id))
                    .Where(t => t.AudioAnalysis == null)
                    .CountAsync(cancellationToken)
                : 0;

            var inaccessiblePathCount = await _context.ScanIssues
                .Where(s => s.LibraryId == library.Id)
                .CountAsync(cancellationToken);

            var pendingStatuses = new[] { BackgroundTaskStatus.Pending, BackgroundTaskStatus.InProgress, BackgroundTaskStatus.WaitingForRetry };
            var pendingTaskCount = await _context.BackgroundTasks
                .Where(t => pendingStatuses.Contains(t.Status))
                .Where(t => t.TargetEntityId != null && linkedMediaIds.Contains(t.TargetEntityId.Value))
                .CountAsync(cancellationToken);

            var failedTaskCount = await _context.BackgroundTasks
                .Where(t => t.Status == BackgroundTaskStatus.Failed)
                .Where(t => t.TargetEntityId != null && linkedMediaIds.Contains(t.TargetEntityId.Value))
                .CountAsync(cancellationToken);

            result.Add(new LibraryHealthSummaryDto
            {
                LibraryId = library.Id,
                LibraryTitle = library.Title,
                MediaType = library.MediaType,
                TotalMediaCount = totalMediaCount,
                MediaMissingPicturesCount = mediaMissingPicturesCount,
                MediaMissingExternalIdCount = mediaMissingExternalIdCount,
                MediaMissingMetadataCount = mediaMissingMetadataCount,
                MediaWithoutFilesCount = mediaWithoutFilesCount,
                StaleMetadataCount = staleMetadataCount,
                TotalIndexedFileCount = totalIndexedFileCount,
                OrphanIndexedFileCount = orphanCount,
                UnidentifiedIndexedFileCount = unidentifiedCount,
                MissingFileMetadataCount = missingFileMetadataCount,
                MissingHlsSegmentsCount = missingHlsSegmentsCount,
                MissingAudioAnalysisCount = missingAudioAnalysisCount,
                InaccessiblePathCount = inaccessiblePathCount,
                PendingBackgroundTaskCount = pendingTaskCount,
                FailedBackgroundTaskCount = failedTaskCount
            });
        }

        return result;
    }
}
