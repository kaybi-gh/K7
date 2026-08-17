using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Diagnostics;
using Microsoft.Extensions.Options;

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
    private readonly PathsConfiguration _paths;

    public GetDiagnosticsSummaryQueryHandler(
        IApplicationDbContext context,
        IOptions<PathsConfiguration> pathsOptions)
    {
        _context = context;
        _paths = pathsOptions.Value;
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
        var missingChaptersCounts = await GetMissingChaptersCountsAsync(cancellationToken);
        var missingThemeSongCounts = await ThemeSongDiagnosticHelper.GetMissingThemeCountsByLibraryAsync(
            _context, _paths, cancellationToken);
        var missingIntroOutroCounts = await IntroOutroDiagnosticHelper.GetMissingIntroOutroCountsByLibraryAsync(
            _context, cancellationToken);
        var inaccessiblePathCounts = await GetInaccessiblePathCountsAsync(cancellationToken);
        var duplicateExternalIdCounts = await DuplicateMediaDiagnosticHelper.GetDuplicateExternalIdCountsByLibraryAsync(
            _context, cancellationToken);
        var suspectedDuplicateCounts = await DuplicateMediaDiagnosticHelper.GetSuspectedDuplicateCountsByLibraryAsync(
            _context, cancellationToken);
        var mediaWithoutFilesCounts = await GetMediaWithoutFilesCountsAsync(cancellationToken);
        var missingMembersCounts = await GetMissingMembersCountsAsync(cancellationToken);

        var musicLibraryIds = libraries
            .Where(l => l.MediaType == LibraryMediaType.Music)
            .Select(l => l.Id)
            .ToList();

        var utcNow = DateTimeOffset.UtcNow;
        var missingAudioAnalysisCounts = await GetMissingAudioAnalysisCountsAsync(musicLibraryIds, cancellationToken);
        var linkedMediaStatsByLibrary = await GetLinkedMediaStatsByLibraryAsync(libraries, utcNow, cancellationToken);
        var backgroundTaskStatsByLibrary = await GetBackgroundTaskStatsByLibraryAsync(cancellationToken);

        var result = new List<LibraryHealthSummaryDto>(libraries.Count);

        foreach (var library in libraries)
        {
            var linkedMediaStats = linkedMediaStatsByLibrary[library.Id];
            var backgroundTaskStats = backgroundTaskStatsByLibrary.GetValueOrDefault(
                library.Id,
                new BackgroundTaskLibraryStats(0, 0));

            indexedFileStats.TryGetValue(library.Id, out var fileStats);
            missingHlsSegmentCounts.TryGetValue(library.Id, out var missingHlsSegmentsCount);
            missingChaptersCounts.TryGetValue(library.Id, out var missingChaptersCount);
            missingThemeSongCounts.TryGetValue(library.Id, out var missingThemeSongCount);
            missingIntroOutroCounts.TryGetValue(library.Id, out var missingIntroOutroCount);
            inaccessiblePathCounts.TryGetValue(library.Id, out var inaccessiblePathCount);
            mediaWithoutFilesCounts.TryGetValue(library.Id, out var mediaWithoutFilesCount);
            missingMembersCounts.TryGetValue(library.Id, out var missingMembersCount);
            missingAudioAnalysisCounts.TryGetValue(library.Id, out var missingAudioAnalysisCount);
            duplicateExternalIdCounts.TryGetValue(library.Id, out var duplicateExternalIdCount);
            suspectedDuplicateCounts.TryGetValue(library.Id, out var suspectedDuplicateMediaCount);

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
                MissingMembersCount = missingMembersCount,
                TotalIndexedFileCount = fileStats?.TotalCount ?? 0,
                OrphanIndexedFileCount = fileStats?.MergedUnlinkedCount ?? 0,
                IdentifiedOrphanIndexedFileCount = fileStats?.IdentifiedOrphanCount ?? 0,
                UnidentifiedIndexedFileCount = fileStats?.UnidentifiedCount ?? 0,
                MissingFileMetadataCount = fileStats?.MissingFileMetadataCount ?? 0,
                MissingHlsSegmentsCount = missingHlsSegmentsCount,
                MissingChaptersCount = missingChaptersCount,
                MissingThemeSongCount = missingThemeSongCount,
                MissingIntroOutroCount = missingIntroOutroCount,
                MissingAudioAnalysisCount = missingAudioAnalysisCount,
                DuplicateExternalIdCount = duplicateExternalIdCount,
                SuspectedDuplicateMediaCount = suspectedDuplicateMediaCount,
                InaccessiblePathCount = inaccessiblePathCount,
                PendingBackgroundTaskCount = backgroundTaskStats.PendingCount,
                FailedBackgroundTaskCount = backgroundTaskStats.FailedCount
            });
        }

        return result;
    }

    private async Task<Dictionary<Guid, IndexedFileLibraryStats>> GetIndexedFileStatsAsync(CancellationToken cancellationToken)
    {
        var baseStats = await _context.IndexedFiles
            .AsNoTracking()
            .GroupBy(f => f.LibraryId)
            .Select(g => new
            {
                LibraryId = g.Key,
                TotalCount = g.Count(),
                MissingFileMetadataCount = g.Count(f => f.FileMetadata == null)
            })
            .ToListAsync(cancellationToken);

        // Owned-type null checks are not reliable inside GroupBy aggregates; use separate queries.
        var identifiedOrphanCounts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId == null && f.Identification != null)
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LibraryId, x => x.Count, cancellationToken);

        var unidentifiedCounts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.Identification == null)
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LibraryId, x => x.Count, cancellationToken);

        var mergedUnlinkedCounts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId == null || f.Identification == null)
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LibraryId, x => x.Count, cancellationToken);

        return baseStats.ToDictionary(
            s => s.LibraryId,
            s => new IndexedFileLibraryStats(
                s.LibraryId,
                s.TotalCount,
                identifiedOrphanCounts.GetValueOrDefault(s.LibraryId),
                unidentifiedCounts.GetValueOrDefault(s.LibraryId),
                mergedUnlinkedCounts.GetValueOrDefault(s.LibraryId),
                s.MissingFileMetadataCount));
    }

    private async Task<Dictionary<Guid, int>> GetMissingHlsSegmentCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.FileMetadata != null && f.FileMetadata.Type == FileType.Video)
            .Where(f => _context.Libraries.Any(l =>
                l.Id == f.LibraryId && l.TransmuxingEnabled && l.PeerServerId == null))
            .Where(f => !_context.HlsSegments.Any(s => s.IndexedFileId == f.Id))
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> GetMissingChaptersCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.FileMetadata != null && f.FileMetadata.Type == FileType.Video)
            .Where(f => _context.Libraries.Any(l => l.Id == f.LibraryId && l.ChapterExtractionEnabled))
            .Where(f => _context.FileMetadatas.OfType<VideoFileMetadata>()
                .Any(m => m.Id == f.FileMetadata!.Id && m.Chapters == null))
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
        // Leaf types that should have a direct IndexedFile / RemoteIndexedFile row.
        // Parent aggregates (serie / season / album / artist) are derived from children and
        // correctly lack their own file rows.
        MediaType[] leafTypes =
        [
            MediaType.Movie,
            MediaType.SerieEpisode,
            MediaType.MusicTrack
        ];

        var counts = await (
            from a in _context.MediaLibraryAvailabilities.AsNoTracking()
            where !_context.Libraries.Any(l => l.Id == a.LibraryId && l.PeerServerId != null)
            join m in _context.Medias.AsNoTracking() on a.MediaId equals m.Id
            where leafTypes.Contains(m.Type)
            where !_context.IndexedFiles.Any(f => f.MediaId == a.MediaId)
                && !_context.RemoteIndexedFiles.Any(r => r.MediaId == a.MediaId)
            group a by a.LibraryId into g
            select new { LibraryId = g.Key, Count = g.Count() }
        ).ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> GetMissingMembersCountsAsync(CancellationToken cancellationToken)
    {
        // Mirror GetDiagnosticItems / DiagnosticIssueEntityResolver: MusicArtists with no PersonRoles,
        // attributed to libraries via albums that have IndexedFiles.
        var counts = await (
            from artist in _context.Medias.OfType<MusicArtist>().AsNoTracking()
            where !artist.PersonRoles.Any()
            from album in _context.Medias.OfType<MusicAlbum>().AsNoTracking()
            where album.ArtistId == artist.Id
            join file in _context.IndexedFiles.AsNoTracking() on album.Id equals file.MediaId
            where !_context.Libraries.Any(l => l.Id == file.LibraryId && l.PeerServerId != null)
            select new { ArtistId = artist.Id, file.LibraryId }
        )
            .Distinct()
            .GroupBy(x => x.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Select(x => x.ArtistId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
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

    private async Task<Dictionary<Guid, LinkedMediaLibraryStats>> GetLinkedMediaStatsByLibraryAsync(
        IReadOnlyList<LibrarySnapshot> libraries,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var staleThresholds = libraries.ToDictionary(
            l => l.Id,
            l => MetadataStalenessHelper.GetStalenessThresholdUtc(l.MetadataRefreshIntervalDays, utcNow));

        var pairs = await LocalAvailabilityPairs()
            .Distinct()
            .ToListAsync(cancellationToken);

        var statsByLibrary = libraries.ToDictionary(
            l => l.Id,
            _ => new LinkedMediaStatsAccumulator());

        if (pairs.Count == 0)
        {
            return statsByLibrary.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToStats());
        }

        var mediaIds = pairs.Select(p => p.MediaId).Distinct().ToList();

        var mediaFlags = await _context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new
            {
                m.Id,
                m.Type,
                m.LastMetadataRefreshedAt,
                HasExternalIds = m.ExternalIds.Any(),
                HasGenre = m.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            })
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var pictureTypes = await _context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.MediaId != null && mediaIds.Contains(p.MediaId.Value))
            .Select(p => new { Id = p.MediaId!.Value, p.Type })
            .ToListAsync(cancellationToken);

        var picturesByMedia = pictureTypes
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Type).Distinct().ToHashSet());

        var seenByLibrary = libraries.ToDictionary(l => l.Id, _ => new HashSet<Guid>());

        foreach (var pair in pairs)
        {
            if (!seenByLibrary.TryGetValue(pair.LibraryId, out var seen) || !seen.Add(pair.MediaId))
                continue;

            if (!mediaFlags.TryGetValue(pair.MediaId, out var flags))
                continue;

            var stats = statsByLibrary[pair.LibraryId];
            stats.TotalMediaCount++;

            var expectedPictures = GetExpectedPictureTypes(flags.Type);
            if (expectedPictures.Count > 0)
            {
                var mediaPictureTypes = picturesByMedia.GetValueOrDefault(pair.MediaId);
                if (mediaPictureTypes is null || expectedPictures.Any(t => !mediaPictureTypes.Contains(t)))
                    stats.MediaMissingPicturesCount++;
            }

            var isRefreshable = flags.Type is MediaType.Movie or MediaType.Serie or MediaType.MusicAlbum or MediaType.MusicArtist;
            var isEnrichable = flags.Type is MediaType.Movie or MediaType.Serie or MediaType.MusicAlbum;

            if (isEnrichable && !flags.HasExternalIds)
                stats.MediaMissingExternalIdCount++;

            if (flags.HasExternalIds && !flags.HasGenre)
                stats.MediaMissingMetadataCount++;

            if (isRefreshable
                && staleThresholds.TryGetValue(pair.LibraryId, out var threshold)
                && threshold is not null
                && (flags.LastMetadataRefreshedAt is null || flags.LastMetadataRefreshedAt < threshold))
            {
                stats.StaleMetadataCount++;
            }
        }

        return statsByLibrary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToStats());
    }

    private async Task<Dictionary<Guid, BackgroundTaskLibraryStats>> GetBackgroundTaskStatsByLibraryAsync(
        CancellationToken cancellationToken)
    {
        var pairsQuery = LocalAvailabilityPairs().Distinct();

        var pendingCounts = await _context.BackgroundTasks
            .AsNoTracking()
            .Where(t => PendingBackgroundTaskStatuses.Contains(t.Status) && t.TargetEntityId != null)
            .Join(
                pairsQuery,
                t => t.TargetEntityId!.Value,
                p => p.MediaId,
                (_, p) => p.LibraryId)
            .GroupBy(libraryId => libraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LibraryId, x => x.Count, cancellationToken);

        var failedCounts = await _context.BackgroundTasks
            .AsNoTracking()
            .Where(t => t.Status == BackgroundTaskStatus.Failed && t.TargetEntityId != null)
            .Join(
                pairsQuery,
                t => t.TargetEntityId!.Value,
                p => p.MediaId,
                (_, p) => p.LibraryId)
            .GroupBy(libraryId => libraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LibraryId, x => x.Count, cancellationToken);

        var libraryIds = pendingCounts.Keys.Union(failedCounts.Keys);

        return libraryIds.ToDictionary(
            id => id,
            id => new BackgroundTaskLibraryStats(
                pendingCounts.GetValueOrDefault(id),
                failedCounts.GetValueOrDefault(id)));
    }

    private IQueryable<MediaLibraryPairProjection> LocalAvailabilityPairs() =>
        _context.MediaLibraryAvailabilities
            .AsNoTracking()
            .Where(a => !_context.Libraries.Any(l => l.Id == a.LibraryId && l.PeerServerId != null))
            .Select(a => new MediaLibraryPairProjection { LibraryId = a.LibraryId, MediaId = a.MediaId });

    private sealed class LinkedMediaStatsAccumulator
    {
        public int TotalMediaCount { get; set; }
        public int MediaMissingPicturesCount { get; set; }
        public int MediaMissingExternalIdCount { get; set; }
        public int MediaMissingMetadataCount { get; set; }
        public int StaleMetadataCount { get; set; }

        public LinkedMediaLibraryStats ToStats() => new(
            TotalMediaCount,
            MediaMissingPicturesCount,
            MediaMissingExternalIdCount,
            MediaMissingMetadataCount,
            StaleMetadataCount);
    }

    private sealed record LibrarySnapshot(
        Guid Id,
        string Title,
        LibraryMediaType MediaType,
        int? MetadataRefreshIntervalDays);

    private static IReadOnlyList<MetadataPictureType> GetExpectedPictureTypes(MediaType type) => type switch
    {
        MediaType.Movie => [MetadataPictureType.Poster, MetadataPictureType.Backdrop],
        MediaType.Serie => [MetadataPictureType.Poster, MetadataPictureType.Backdrop],
        MediaType.SerieSeason => [MetadataPictureType.Poster],
        MediaType.SerieEpisode => [MetadataPictureType.Still],
        MediaType.MusicAlbum => [MetadataPictureType.Cover],
        _ => []
    };

    private sealed record IndexedFileLibraryStats(
        Guid LibraryId,
        int TotalCount,
        int IdentifiedOrphanCount,
        int UnidentifiedCount,
        int MergedUnlinkedCount,
        int MissingFileMetadataCount);

    private sealed record LinkedMediaLibraryStats(
        int TotalMediaCount,
        int MediaMissingPicturesCount,
        int MediaMissingExternalIdCount,
        int MediaMissingMetadataCount,
        int StaleMetadataCount);

    private sealed record BackgroundTaskLibraryStats(int PendingCount, int FailedCount);
}
