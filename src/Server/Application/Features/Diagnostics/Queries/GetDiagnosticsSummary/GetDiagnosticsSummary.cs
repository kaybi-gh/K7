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

        // Owned-type null checks are not reliable inside GroupBy aggregates. Filter first,
        // then count. Merged unlinked = identified + unidentified (MediaId == null partition).
        var identifiedOrphanCounts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId == null && f.Identification != null)
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LibraryId, x => x.Count, cancellationToken);

        var unidentifiedCounts = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId == null && f.Identification == null)
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LibraryId, x => x.Count, cancellationToken);

        return baseStats.ToDictionary(
            s => s.LibraryId,
            s =>
            {
                var identified = identifiedOrphanCounts.GetValueOrDefault(s.LibraryId);
                var unidentified = unidentifiedCounts.GetValueOrDefault(s.LibraryId);
                return new IndexedFileLibraryStats(
                    s.LibraryId,
                    s.TotalCount,
                    identified,
                    unidentified,
                    identified + unidentified,
                    s.MissingFileMetadataCount);
            });
    }

    private async Task<Dictionary<Guid, int>> GetMissingHlsSegmentCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await (
            from file in _context.IndexedFiles.AsNoTracking()
            join library in _context.Libraries.AsNoTracking() on file.LibraryId equals library.Id
            where file.FileMetadata != null
                && file.FileMetadata.Type == FileType.Video
                && library.TransmuxingEnabled
                && library.PeerServerId == null
                && !_context.HlsSegments.Any(s => s.IndexedFileId == file.Id)
            group file by file.LibraryId into g
            select new { LibraryId = g.Key, Count = g.Count() }
        ).ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> GetMissingChaptersCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await (
            from file in _context.IndexedFiles.AsNoTracking()
            join library in _context.Libraries.AsNoTracking() on file.LibraryId equals library.Id
            where file.FileMetadata != null
                && file.FileMetadata.Type == FileType.Video
                && library.ChapterExtractionEnabled
            join video in _context.FileMetadatas.OfType<VideoFileMetadata>().AsNoTracking()
                on file.FileMetadata!.Id equals video.Id
            where video.Chapters == null
            group file by file.LibraryId into g
            select new { LibraryId = g.Key, Count = g.Count() }
        ).ToListAsync(cancellationToken);

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
            join library in _context.Libraries.AsNoTracking() on a.LibraryId equals library.Id
            where library.PeerServerId == null
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
            join album in _context.Medias.OfType<MusicAlbum>().AsNoTracking()
                on artist.Id equals album.ArtistId
            join file in _context.IndexedFiles.AsNoTracking() on album.Id equals file.MediaId
            join library in _context.Libraries.AsNoTracking() on file.LibraryId equals library.Id
            where library.PeerServerId == null
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
        var pairs = LocalAvailabilityPairs();
        var totals = await CountDistinctPairsByLibraryAsync(pairs, cancellationToken);
        var missingPictures = await CountMissingPicturesByLibraryAsync(pairs, cancellationToken);
        var missingExternalIds = await CountMissingExternalIdsByLibraryAsync(pairs, cancellationToken);
        var missingMetadata = await CountMissingMetadataByLibraryAsync(pairs, cancellationToken);
        var staleMetadata = await CountStaleMetadataByLibraryAsync(pairs, libraries, utcNow, cancellationToken);

        return libraries.ToDictionary(
            l => l.Id,
            l => new LinkedMediaLibraryStats(
                totals.GetValueOrDefault(l.Id),
                missingPictures.GetValueOrDefault(l.Id),
                missingExternalIds.GetValueOrDefault(l.Id),
                missingMetadata.GetValueOrDefault(l.Id),
                staleMetadata.GetValueOrDefault(l.Id)));
    }

    private async Task<Dictionary<Guid, int>> CountDistinctPairsByLibraryAsync(
        IQueryable<MediaLibraryPairProjection> pairs,
        CancellationToken cancellationToken)
    {
        var counts = await pairs
            .Select(p => new { p.LibraryId, p.MediaId })
            .Distinct()
            .GroupBy(p => p.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> CountMissingPicturesByLibraryAsync(
        IQueryable<MediaLibraryPairProjection> pairs,
        CancellationToken cancellationToken)
    {
        var counts = await (
            from p in pairs
            join m in _context.Medias.AsNoTracking() on p.MediaId equals m.Id
            where (m.Type == MediaType.Movie || m.Type == MediaType.Serie)
                    && (!_context.MetadataPictures.Any(pic => pic.MediaId == m.Id && pic.Type == MetadataPictureType.Poster)
                        || !_context.MetadataPictures.Any(pic => pic.MediaId == m.Id && pic.Type == MetadataPictureType.Backdrop))
                || m.Type == MediaType.SerieSeason
                    && !_context.MetadataPictures.Any(pic => pic.MediaId == m.Id && pic.Type == MetadataPictureType.Poster)
                || m.Type == MediaType.SerieEpisode
                    && !_context.MetadataPictures.Any(pic => pic.MediaId == m.Id && pic.Type == MetadataPictureType.Still)
                || m.Type == MediaType.MusicAlbum
                    && !_context.MetadataPictures.Any(pic => pic.MediaId == m.Id && pic.Type == MetadataPictureType.Cover)
            select new { p.LibraryId, p.MediaId }
        )
            .Distinct()
            .GroupBy(p => p.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> CountMissingExternalIdsByLibraryAsync(
        IQueryable<MediaLibraryPairProjection> pairs,
        CancellationToken cancellationToken)
    {
        var counts = await (
            from p in pairs
            join m in _context.Medias.AsNoTracking() on p.MediaId equals m.Id
            where (m.Type == MediaType.Movie || m.Type == MediaType.Serie || m.Type == MediaType.MusicAlbum)
                && !m.ExternalIds.Any()
            select new { p.LibraryId, p.MediaId }
        )
            .Distinct()
            .GroupBy(p => p.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> CountMissingMetadataByLibraryAsync(
        IQueryable<MediaLibraryPairProjection> pairs,
        CancellationToken cancellationToken)
    {
        var counts = await (
            from p in pairs
            join m in _context.Medias.AsNoTracking() on p.MediaId equals m.Id
            where m.ExternalIds.Any()
                && !m.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            select new { p.LibraryId, p.MediaId }
        )
            .Distinct()
            .GroupBy(p => p.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }

    private async Task<Dictionary<Guid, int>> CountStaleMetadataByLibraryAsync(
        IQueryable<MediaLibraryPairProjection> pairs,
        IReadOnlyList<LibrarySnapshot> libraries,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var stale = new Dictionary<Guid, int>();

        foreach (var group in libraries.GroupBy(l => l.MetadataRefreshIntervalDays))
        {
            var threshold = MetadataStalenessHelper.GetStalenessThresholdUtc(group.Key, utcNow);
            if (threshold is null)
                continue;

            var libraryIds = group.Select(l => l.Id).ToList();
            var counts = await (
                from p in pairs
                where libraryIds.Contains(p.LibraryId)
                join m in _context.Medias.AsNoTracking() on p.MediaId equals m.Id
                where (m.Type == MediaType.Movie
                        || m.Type == MediaType.Serie
                        || m.Type == MediaType.MusicAlbum
                        || m.Type == MediaType.MusicArtist)
                    && (m.LastMetadataRefreshedAt == null || m.LastMetadataRefreshedAt < threshold)
                select new { p.LibraryId, p.MediaId }
            )
                .Distinct()
                .GroupBy(p => p.LibraryId)
                .Select(g => new { LibraryId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            foreach (var row in counts)
                stale[row.LibraryId] = row.Count;
        }

        return stale;
    }

    private async Task<Dictionary<Guid, BackgroundTaskLibraryStats>> GetBackgroundTaskStatsByLibraryAsync(
        CancellationToken cancellationToken)
    {
        var tasks = await _context.BackgroundTasks
            .AsNoTracking()
            .Where(t => t.TargetEntityId != null
                && (PendingBackgroundTaskStatuses.Contains(t.Status)
                    || t.Status == BackgroundTaskStatus.Failed))
            .Select(t => new { t.Status, MediaId = t.TargetEntityId!.Value })
            .ToListAsync(cancellationToken);

        if (tasks.Count == 0)
            return [];

        var mediaIds = tasks.Select(t => t.MediaId).Distinct().ToList();
        var pairs = await LocalAvailabilityPairs()
            .Where(p => mediaIds.Contains(p.MediaId))
            .Select(p => new { p.LibraryId, p.MediaId })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (pairs.Count == 0)
            return [];

        var librariesByMedia = pairs
            .GroupBy(p => p.MediaId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.LibraryId).ToList());

        var pendingCounts = new Dictionary<Guid, int>();
        var failedCounts = new Dictionary<Guid, int>();

        foreach (var task in tasks)
        {
            if (!librariesByMedia.TryGetValue(task.MediaId, out var libraryIds))
                continue;

            var bucket = PendingBackgroundTaskStatuses.Contains(task.Status)
                ? pendingCounts
                : failedCounts;

            foreach (var libraryId in libraryIds)
                bucket[libraryId] = bucket.GetValueOrDefault(libraryId) + 1;
        }

        return pendingCounts.Keys.Union(failedCounts.Keys).ToDictionary(
            id => id,
            id => new BackgroundTaskLibraryStats(
                pendingCounts.GetValueOrDefault(id),
                failedCounts.GetValueOrDefault(id)));
    }

    private IQueryable<MediaLibraryPairProjection> LocalAvailabilityPairs() =>
        from a in _context.MediaLibraryAvailabilities.AsNoTracking()
        join library in _context.Libraries.AsNoTracking() on a.LibraryId equals library.Id
        where library.PeerServerId == null
        select new MediaLibraryPairProjection { LibraryId = a.LibraryId, MediaId = a.MediaId };

    private sealed record LibrarySnapshot(
        Guid Id,
        string Title,
        LibraryMediaType MediaType,
        int? MetadataRefreshIntervalDays);

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
