using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Shared.Diagnostics;
using K7.Shared.Dtos.Diagnostics;
using K7.Shared.Dtos.Entities;
using K7.Shared.Navigation;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticItems;

[Authorize(Roles = Roles.Administrator)]
public record GetDiagnosticItemsQuery : IRequest<PaginatedList<DiagnosticItemDto>>
{
    public Guid? LibraryId { get; init; }
    public DiagnosticEntityType? EntityType { get; init; }
    public DiagnosticIssue? Issue { get; init; }
    public IReadOnlyCollection<DiagnosticIssue>? Issues { get; init; }
    /// <summary>
    /// When set, keeps only rows whose severity matches.
    /// </summary>
    public DiagnosticSeverity? Severity { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
}

public class GetDiagnosticItemsQueryHandler : IRequestHandler<GetDiagnosticItemsQuery, PaginatedList<DiagnosticItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly PathsConfiguration _paths;

    public GetDiagnosticItemsQueryHandler(
        IApplicationDbContext context,
        IOptions<PathsConfiguration> pathsOptions)
    {
        _context = context;
        _paths = pathsOptions.Value;
    }

    public async Task<PaginatedList<DiagnosticItemDto>> Handle(GetDiagnosticItemsQuery request, CancellationToken cancellationToken)
    {
        if (request.EntityType == DiagnosticEntityType.IndexedFile)
            return await GetIndexedFileIssuesPaginatedAsync(request, cancellationToken);

        if (request.EntityType == DiagnosticEntityType.Library)
            return await GetScanIssuesPaginatedAsync(request, cancellationToken);

        if (request.EntityType == DiagnosticEntityType.Media)
            return await GetMediaIssuesPaginatedAsync(request, cancellationToken);

        var sourceRequest = request with { PageNumber = 1, PageSize = request.PageNumber * request.PageSize };
        var indexedFiles = await GetIndexedFileIssuesPaginatedAsync(sourceRequest, cancellationToken);
        var scanIssues = await GetScanIssuesPaginatedAsync(sourceRequest, cancellationToken);
        var mediaIssues = await GetMediaIssuesPaginatedAsync(sourceRequest, cancellationToken);
        var totalCount = indexedFiles.TotalCount + scanIssues.TotalCount + mediaIssues.TotalCount;
        var paged = indexedFiles.Items
            .Concat(scanIssues.Items)
            .Concat(mediaIssues.Items)
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.EntityName)
            .ThenBy(item => item.Issues[0])
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PaginatedList<DiagnosticItemDto>(paged, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<PaginatedList<DiagnosticItemDto>> GetIndexedFileIssuesPaginatedAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken)
    {
        var query = BuildIndexedFileIssueRowsQuery(request);
        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.Severity)
            .ThenBy(row => row.EntityName)
            .ThenBy(row => row.Issue)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var identificationById = await LoadIdentificationsByIndexedFileIdAsync(
            rows.Select(row => row.EntityId),
            cancellationToken);

        return new PaginatedList<DiagnosticItemDto>(
            rows.Select(row => MapIndexedFileIssue(row, identificationById.GetValueOrDefault(row.EntityId))).ToList(),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    private async Task<PaginatedList<DiagnosticItemDto>> GetScanIssuesPaginatedAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ScanIssues
            .AsNoTracking()
            .Where(s => !_context.Libraries.Any(l => l.Id == s.LibraryId && l.PeerServerId != null));

        if (request.LibraryId.HasValue)
            query = query.Where(s => s.LibraryId == request.LibraryId.Value);

        if (request.Issue.HasValue && request.Issue.Value != DiagnosticIssue.InaccessiblePath
            || request.Issues is { Count: > 0 } && !request.Issues.Contains(DiagnosticIssue.InaccessiblePath))
        {
            return new PaginatedList<DiagnosticItemDto>([], 0, request.PageNumber, request.PageSize);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.Path)
            .Select(s => new DiagnosticItemDto
            {
                EntityId = s.Id,
                EntityName = s.Path,
                EntityType = DiagnosticEntityType.Library,
                LibraryId = s.LibraryId,
                LibraryTitle = _context.Libraries.Where(l => l.Id == s.LibraryId).Select(l => l.Title).FirstOrDefault() ?? "",
                Issues = new List<DiagnosticIssue> { DiagnosticIssue.InaccessiblePath },
                Severity = DiagnosticSeverity.Error,
                DetailText = s.ErrorMessage
            })
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<DiagnosticItemDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private IQueryable<IndexedFileIssueRow> BuildIndexedFileIssueRowsQuery(GetDiagnosticItemsQuery request)
    {
        var files = _context.IndexedFiles
            .AsNoTracking()
            .Where(f => !_context.Libraries.Any(l => l.Id == f.LibraryId && l.PeerServerId != null));

        if (request.LibraryId.HasValue)
            files = files.Where(f => f.LibraryId == request.LibraryId.Value);

        var flags = files.Select(f => new
        {
            f.Id,
            f.Name,
            f.Path,
            f.LibraryId,
            LibraryTitle = _context.Libraries.Where(l => l.Id == f.LibraryId).Select(l => l.Title).FirstOrDefault() ?? "",
            IsMergedOrphan = f.MediaId == null,
            HasNoFileMetadata = f.FileMetadata == null,
            HasNoHlsSegments = f.FileMetadata != null
                && f.FileMetadata.Type == FileType.Video
                && _context.Libraries.Any(l => l.Id == f.LibraryId && l.TransmuxingEnabled)
                && !_context.HlsSegments.Any(s => s.IndexedFileId == f.Id),
            HasNoChapters = f.FileMetadata != null
                && f.FileMetadata.Type == FileType.Video
                && _context.Libraries.Any(l => l.Id == f.LibraryId && l.ChapterExtractionEnabled)
                && _context.FileMetadatas.OfType<VideoFileMetadata>()
                    .Any(m => m.Id == f.FileMetadata.Id && m.Chapters == null)
        });

        var query = flags.Where(f => f.IsMergedOrphan).Select(f => new IndexedFileIssueRow
        {
            EntityId = f.Id,
            EntityName = f.Name,
            Path = f.Path,
            LibraryId = f.LibraryId,
            LibraryTitle = f.LibraryTitle,
            Issue = DiagnosticIssue.OrphanFile,
            Severity = DiagnosticSeverity.Error
        })
        .Concat(flags.Where(f => f.HasNoFileMetadata).Select(f => new IndexedFileIssueRow
        {
            EntityId = f.Id,
            EntityName = f.Name,
            Path = f.Path,
            LibraryId = f.LibraryId,
            LibraryTitle = f.LibraryTitle,
            Issue = DiagnosticIssue.MissingFileMetadata,
            Severity = DiagnosticSeverity.Error
        }))
        .Concat(flags.Where(f => f.HasNoHlsSegments).Select(f => new IndexedFileIssueRow
        {
            EntityId = f.Id,
            EntityName = f.Name,
            Path = f.Path,
            LibraryId = f.LibraryId,
            LibraryTitle = f.LibraryTitle,
            Issue = DiagnosticIssue.MissingHlsSegments,
            Severity = DiagnosticSeverity.Info
        }))
        .Concat(flags.Where(f => f.HasNoChapters).Select(f => new IndexedFileIssueRow
        {
            EntityId = f.Id,
            EntityName = f.Name,
            Path = f.Path,
            LibraryId = f.LibraryId,
            LibraryTitle = f.LibraryTitle,
            Issue = DiagnosticIssue.MissingChapters,
            Severity = DiagnosticSeverity.Info
        }));

        if (request.Issue.HasValue)
        {
            var issue = DiagnosticIssueTaxonomy.Canonicalize(request.Issue.Value);
            query = query.Where(row => row.Issue == issue);
        }

        if (request.Issues is { Count: > 0 })
        {
            var allowed = request.Issues.Select(DiagnosticIssueTaxonomy.Canonicalize).Distinct().ToList();
            query = query.Where(row => allowed.Contains(row.Issue));
        }

        if (request.Severity.HasValue)
            query = query.Where(row => row.Severity == request.Severity.Value);

        return query;
    }

    private static DiagnosticItemDto MapIndexedFileIssue(
        IndexedFileIssueRow row,
        MediaIdentificationDto? identification = null) => new()
        {
            EntityId = row.EntityId,
            EntityName = row.EntityName,
            EntityType = DiagnosticEntityType.IndexedFile,
            LibraryId = row.LibraryId,
            LibraryTitle = row.LibraryTitle,
            Issues = [row.Issue],
            Severity = row.Severity,
            DetailText = row.Path,
            Identification = identification
        };

    private async Task<Dictionary<Guid, MediaIdentificationDto?>> LoadIdentificationsByIndexedFileIdAsync(
        IEnumerable<Guid> indexedFileIds,
        CancellationToken cancellationToken)
    {
        var ids = indexedFileIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var rows = await _context.IndexedFiles
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new { f.Id, f.Identification })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.Id,
            row => row.Identification?.ToMediaIdentificationDto());
    }

    private async Task<PaginatedList<DiagnosticItemDto>> GetMediaIssuesPaginatedAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken)
    {
        var sourceRequest = request with { PageNumber = 1, PageSize = request.PageNumber * request.PageSize };
        var mediaPage = await GetMediaIssuePageAsync(sourceRequest, cancellationToken);
        var artistPage = await GetMusicArtistIssuePageAsync(sourceRequest, cancellationToken);
        var totalCount = mediaPage.TotalCount + artistPage.TotalCount;
        var items = mediaPage.Items
            .Concat(artistPage.Items)
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.EntityName)
            .ThenBy(item => item.Issues[0])
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PaginatedList<DiagnosticItemDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private static List<DiagnosticItemDto> ApplyIssueFilters(
        List<DiagnosticItemDto> items,
        GetDiagnosticItemsQuery request)
    {
        IReadOnlyCollection<DiagnosticIssue>? allowed = null;
        if (request.Issue.HasValue && request.Issues is { Count: > 0 })
        {
            var canonicalSingle = DiagnosticIssueTaxonomy.Canonicalize(request.Issue.Value);
            allowed = request.Issues.Select(DiagnosticIssueTaxonomy.Canonicalize).Contains(canonicalSingle)
                ? [canonicalSingle]
                : [];
        }
        else if (request.Issue.HasValue)
            allowed = [DiagnosticIssueTaxonomy.Canonicalize(request.Issue.Value)];
        else if (request.Issues is { Count: > 0 })
            allowed = request.Issues.Select(DiagnosticIssueTaxonomy.Canonicalize).Distinct().ToList();

        if (allowed is { Count: 0 })
            return [];

        IEnumerable<DiagnosticItemDto> filtered = items;
        if (allowed is not null)
        {
            filtered = filtered
                .Select(item =>
                {
                    var issues = item.Issues
                        .Select(DiagnosticIssueTaxonomy.Canonicalize)
                        .Where(allowed.Contains)
                        .Distinct()
                        .ToList();
                    if (issues.Count == 0)
                        return null;

                    // Recompute severity from the issues kept after filtering.
                    var severity = issues.Max(GetIssueSeverity);

                    return item with
                    {
                        Issues = issues,
                        Severity = severity
                    };
                })
                .Where(item => item is not null)
                .Cast<DiagnosticItemDto>();
        }

        if (request.Severity.HasValue)
        {
            var severityFilter = request.Severity.Value;
            filtered = filtered
                .Select(item =>
                {
                    // One entity row can mix severities; keep only issues in the requested band
                    // so a Warning filter never lists Error/Info chips on the same media.
                    var issues = item.Issues
                        .Select(DiagnosticIssueTaxonomy.Canonicalize)
                        .Where(issue => GetIssueSeverity(issue) == severityFilter)
                        .Distinct()
                        .ToList();
                    if (issues.Count == 0)
                        return null;

                    var severity = issues.Max(GetIssueSeverity);
                    if (severity != severityFilter)
                        return null;

                    return item with
                    {
                        Issues = issues,
                        Severity = severity
                    };
                })
                .Where(item => item is not null)
                .Cast<DiagnosticItemDto>();
        }

        return filtered.ToList();
    }

    private static DiagnosticSeverity GetIssueSeverity(DiagnosticIssue issue) =>
        DiagnosticIssueTaxonomy.GetSeverity(issue);

    private async Task<List<DiagnosticItemDto>> GetIndexedFileIssuesAsync(GetDiagnosticItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.IndexedFiles
            .Include(f => f.FileMetadata)
            .AsNoTracking()
            .Where(f => !_context.Libraries.Any(l => l.Id == f.LibraryId && l.PeerServerId != null))
            .AsQueryable();

        if (request.LibraryId.HasValue)
        {
            query = query.Where(f => f.LibraryId == request.LibraryId.Value);
        }

        var files = await query
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Path,
                f.LibraryId,
                LibraryTitle = _context.Libraries.Where(l => l.Id == f.LibraryId).Select(l => l.Title).FirstOrDefault() ?? "",
                IsMergedOrphan = f.MediaId == null,
                HasNoFileMetadata = f.FileMetadata == null,
                HasNoHlsSegments = f.FileMetadata != null
                    && f.FileMetadata.Type == FileType.Video
                    && _context.Libraries.Any(l => l.Id == f.LibraryId && l.TransmuxingEnabled)
                    && !_context.HlsSegments.Any(s => s.IndexedFileId == f.Id),
                HasNoChapters = f.FileMetadata != null
                    && f.FileMetadata.Type == FileType.Video
                    && _context.Libraries.Any(l => l.Id == f.LibraryId && l.ChapterExtractionEnabled)
                    && _context.FileMetadatas.OfType<VideoFileMetadata>()
                        .Any(m => m.Id == f.FileMetadata.Id && m.Chapters == null)
            })
            .Where(f => f.IsMergedOrphan || f.HasNoFileMetadata || f.HasNoHlsSegments || f.HasNoChapters)
            .ToListAsync(cancellationToken);

        return files.Select(f =>
        {
            var issues = new List<DiagnosticIssue>();
            if (f.IsMergedOrphan) issues.Add(DiagnosticIssue.OrphanFile);
            if (f.HasNoFileMetadata) issues.Add(DiagnosticIssue.MissingFileMetadata);
            if (f.HasNoHlsSegments) issues.Add(DiagnosticIssue.MissingHlsSegments);
            if (f.HasNoChapters) issues.Add(DiagnosticIssue.MissingChapters);

            var severity = issues.Max(GetIssueSeverity);

            return new DiagnosticItemDto
            {
                EntityId = f.Id,
                EntityName = f.Name,
                EntityType = DiagnosticEntityType.IndexedFile,
                LibraryId = f.LibraryId,
                LibraryTitle = f.LibraryTitle,
                Issues = issues,
                Severity = severity,
                DetailText = f.Path
            };
        }).ToList();
    }

    private async Task<List<DiagnosticItemDto>> GetMediaIssuesAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? selectedMediaIds = null)
    {
        var libraryMediaQuery = _context.MediaLibraryAvailabilities
            .Where(a => !_context.Libraries.Any(l => l.Id == a.LibraryId && l.PeerServerId != null))
            .AsQueryable();

        if (request.LibraryId.HasValue)
        {
            libraryMediaQuery = libraryMediaQuery.Where(a => a.LibraryId == request.LibraryId.Value);
        }

        if (selectedMediaIds is not null)
            libraryMediaQuery = libraryMediaQuery.Where(a => selectedMediaIds.Contains(a.MediaId));

        var libraryMediaPairs = await libraryMediaQuery
            .Select(a => new { MediaId = a.MediaId, a.LibraryId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var mediaToLibrary = libraryMediaPairs
            .DistinctBy(x => x.MediaId)
            .ToDictionary(x => x.MediaId, x => x.LibraryId);

        // Music tracks are usually linked through IndexedFiles, not MediaLibraryAvailability.
        if (selectedMediaIds is not null)
        {
            var missingIds = selectedMediaIds.Where(id => !mediaToLibrary.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
            {
                var fromFiles = await _context.IndexedFiles
                    .AsNoTracking()
                    .Where(f => f.MediaId != null && missingIds.Contains(f.MediaId.Value))
                    .Where(f => !_context.Libraries.Any(l => l.Id == f.LibraryId && l.PeerServerId != null))
                    .Select(f => new { MediaId = f.MediaId!.Value, f.LibraryId })
                    .ToListAsync(cancellationToken);

                foreach (var pair in fromFiles.DistinctBy(x => x.MediaId))
                    mediaToLibrary[pair.MediaId] = pair.LibraryId;
            }
        }

        var mediaIds = mediaToLibrary.Keys.ToHashSet();

        var libraryInfo = await _context.Libraries
            .Where(l => mediaToLibrary.Values.Distinct().Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new { l.Title, l.MetadataRefreshIntervalDays }, cancellationToken);

        var medias = await _context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.Type,
                m.LastMetadataRefreshedAt,
                HasExternalIds = m.ExternalIds.Any(),
                GenreCount = m.MetadataTags.Count(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre),
                IsMusicTrack = m is MusicTrack,
                HasAudioAnalysis = m is MusicTrack && ((MusicTrack)m).AudioAnalysis != null
            })
            .ToListAsync(cancellationToken);

        var episodeNavById = await _context.Medias.OfType<SerieEpisode>()
            .Where(e => mediaIds.Contains(e.Id))
            .Select(e => new { e.Id, e.SerieId, SeasonNumber = e.Season.SeasonNumber, e.EpisodeNumber })
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var seasonNavById = await _context.Medias.OfType<SerieSeason>()
            .Where(s => mediaIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SerieId, s.SeasonNumber })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var trackNavById = await _context.Medias.OfType<MusicTrack>()
            .Where(t => mediaIds.Contains(t.Id))
            .Select(t => new { t.Id, t.AlbumId })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var albumIds = trackNavById.Values.Select(t => t.AlbumId).Distinct().ToList();
        var albumMetadataById = albumIds.Count == 0
            ? new Dictionary<Guid, AlbumDiagnosticInfo>()
            : await _context.Medias.OfType<MusicAlbum>()
                .AsNoTracking()
                .Where(a => albumIds.Contains(a.Id))
                .Select(a => new AlbumDiagnosticInfo
                {
                    Id = a.Id,
                    HasExternalIds = a.ExternalIds.Any(),
                    GenreCount = a.MetadataTags.Count(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                })
                .ToDictionaryAsync(a => a.Id, cancellationToken);

        var pictureTypes = await _context.MetadataPictures
            .Where(p => p.MediaId != null && mediaIds.Contains(p.MediaId.Value))
            .Select(p => new { Id = p.MediaId!.Value, p.Type })
            .ToListAsync(cancellationToken);

        var picturesByMedia = pictureTypes
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Type).Distinct().ToList());

        var serieIds = medias.Where(m => m.Type == MediaType.Serie).Select(m => m.Id).ToList();
        var missingThemeSerieIds = serieIds.Count == 0
            ? new HashSet<Guid>()
            : await ThemeSongDiagnosticHelper.GetMissingThemeSerieIdsAsync(
                _context, _paths, request.LibraryId, serieIds, cancellationToken: cancellationToken);

        var episodeIds = medias.Where(m => m.Type == MediaType.SerieEpisode).Select(m => m.Id).ToList();
        var missingIntroOutroEpisodeIds = episodeIds.Count == 0
            ? new HashSet<Guid>()
            : await IntroOutroDiagnosticHelper.GetMissingIntroOutroEpisodeIdsAsync(
                _context, request.LibraryId, episodeIds, cancellationToken: cancellationToken);

        // Duplicate detection is a signal-only safety net: media creation is a find-or-create
        // over a fuzzy identity, so duplicates cannot be prevented by construction. See
        // DuplicateMediaDiagnosticHelper for the rationale and the two heuristics.
        var duplicateExternalIdMediaIds = await DuplicateMediaDiagnosticHelper.GetDuplicateExternalIdMediaIdsAsync(
            _context, mediaIds, cancellationToken);
        var suspectedDuplicateMediaIds = await DuplicateMediaDiagnosticHelper.GetSuspectedDuplicateMediaIdsAsync(
            _context, request.LibraryId, mediaIds, cancellationToken);

        return medias.Select(m =>
        {
            var libraryId = mediaToLibrary.GetValueOrDefault(m.Id);
            var libInfo = libraryInfo.GetValueOrDefault(libraryId);
            var threshold = libInfo?.MetadataRefreshIntervalDays;

            var mediaPictureTypes = picturesByMedia.GetValueOrDefault(m.Id, []);
            var expectedPictures = GetExpectedPictureTypes(m.Type);
            var missingPictures = expectedPictures.Except(mediaPictureTypes).ToList();

            var issues = new List<DiagnosticIssue>();
            if (missingPictures.Count > 0) issues.Add(DiagnosticIssue.MissingPictures);

            var isEnrichableMedia = m.Type is MediaType.Movie or MediaType.Serie or MediaType.MusicAlbum;
            if (!m.HasExternalIds && isEnrichableMedia) issues.Add(DiagnosticIssue.MissingExternalId);
            if (!m.HasExternalIds && !isEnrichableMedia && m.GenreCount == 0) issues.Add(DiagnosticIssue.MissingMetadata);
            if (m.HasExternalIds && m.GenreCount == 0) issues.Add(DiagnosticIssue.MissingMetadata);

            if (m.IsMusicTrack
                && trackNavById.TryGetValue(m.Id, out var trackRef)
                && albumMetadataById.TryGetValue(trackRef.AlbumId, out var albumInfo)
                && (albumInfo.GenreCount > 0 || albumInfo.HasExternalIds))
            {
                issues.Remove(DiagnosticIssue.MissingMetadata);
            }

            var isRefreshable = m.Type is MediaType.Movie or MediaType.Serie or MediaType.MusicAlbum or MediaType.MusicArtist;
            var isStale = isRefreshable
                && MetadataStalenessHelper.IsStale(m.LastMetadataRefreshedAt, threshold, DateTimeOffset.UtcNow);
            if (isStale) issues.Add(DiagnosticIssue.StaleMetadata);

            if (m.IsMusicTrack && !m.HasAudioAnalysis) issues.Add(DiagnosticIssue.MissingAudioAnalysis);

            if (m.Type == MediaType.Serie && missingThemeSerieIds.Contains(m.Id))
                issues.Add(DiagnosticIssue.MissingThemeSong);

            if (m.Type == MediaType.SerieEpisode && missingIntroOutroEpisodeIds.Contains(m.Id))
                issues.Add(DiagnosticIssue.MissingIntroOutro);

            if (duplicateExternalIdMediaIds.Contains(m.Id))
                issues.Add(DiagnosticIssue.DuplicateExternalId);

            if (suspectedDuplicateMediaIds.Contains(m.Id))
                issues.Add(DiagnosticIssue.SuspectedDuplicateMedia);

            if (issues.Count == 0) return null;

            episodeNavById.TryGetValue(m.Id, out var episodeNav);
            seasonNavById.TryGetValue(m.Id, out var seasonNav);
            trackNavById.TryGetValue(m.Id, out var trackNav);

            var severity = issues.Max(GetIssueSeverity);

            return new DiagnosticItemDto
            {
                EntityId = m.Id,
                EntityName = m.Title ?? "(untitled)",
                EntityType = DiagnosticEntityType.Media,
                LibraryId = libraryId,
                LibraryTitle = libInfo?.Title ?? "",
                Issues = issues,
                Severity = severity,
                MediaType = m.Type,
                MediaUrl = MediaPageUrls.Build(
                    m.Type,
                    m.Id,
                    episodeNav?.SerieId ?? seasonNav?.SerieId,
                    episodeNav?.SeasonNumber ?? seasonNav?.SeasonNumber,
                    episodeNav?.EpisodeNumber,
                    trackNav?.AlbumId),
                MissingPictureTypes = missingPictures.Count > 0
                    ? missingPictures.Select(p => p.ToString()).ToList()
                    : null,
                LastMetadataRefreshedAt = m.LastMetadataRefreshedAt,
                MetadataRefreshIntervalDays = threshold
            };
        })
        .Where(dto => dto is not null)
        .Cast<DiagnosticItemDto>()
        .ToList();
    }

    private async Task<PaginatedList<DiagnosticItemDto>> GetMediaIssuePageAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken)
    {
        // MissingThemeSong depends on filesystem checks; candidate SQL alone over-counts
        // (series with intros that already have a theme) and breaks filter TotalCount / bulk fix UI.
        if (IsOnlyIssueFilter(request, DiagnosticIssue.MissingThemeSong))
            return await GetMissingThemeSongIssuePageAsync(request, cancellationToken);

        if (IsOnlyIssueFilter(request, DiagnosticIssue.MissingIntroOutro))
            return await GetMissingIntroOutroIssuePageAsync(request, cancellationToken);

        if (request.Issue is { } singleIssue && !IsMediaCatalogIssue(singleIssue))
            return EmptyPage(request);

        if (request.Issues is { Count: > 0 } && !request.Issues.Any(IsMediaCatalogIssue))
            return EmptyPage(request);

        var availability = _context.MediaLibraryAvailabilities
            .Where(a => !_context.Libraries.Any(l => l.Id == a.LibraryId && l.PeerServerId != null));

        if (request.LibraryId.HasValue)
            availability = availability.Where(a => a.LibraryId == request.LibraryId.Value);

        var duplicateExternalIdMediaIds = DuplicateMediaDiagnosticHelper.QueryDuplicateExternalIdMediaIds(_context);
        var suspectedDuplicateMediaIds = DuplicateMediaDiagnosticHelper.QuerySuspectedDuplicateMediaIds(_context, request.LibraryId);
        var activeIssues = GetRequestedMediaCatalogIssues(request);
        var staleMediaIds = await GetStaleRefreshableMediaIdsAsync(availability, cancellationToken);

        // When an issue filter is set, narrow candidates to that issue before Skip/Take.
        // Paging the broad "any issue" set then filtering leaves empty pages and an inflated TotalCount
        // (e.g. MissingExternalId buried after many MissingPictures rows).
        var candidateIdQuery = activeIssues is null
            ? BuildBroadMediaCandidateIdQuery(
                availability, request.LibraryId, duplicateExternalIdMediaIds, suspectedDuplicateMediaIds, staleMediaIds)
            : BuildFilteredMediaCandidateIdQuery(
                availability, request.LibraryId, activeIssues, duplicateExternalIdMediaIds, suspectedDuplicateMediaIds, staleMediaIds);

        var candidateIds = _context.Medias
            .AsNoTracking()
            .Where(m => candidateIdQuery.Contains(m.Id))
            .Select(m => new { m.Id, Name = m.Title ?? "(untitled)" });

        // Single-issue filters and unfiltered views both page by media id (one row per media).
        // Multiple issues stay on the same row; the UI lists them all.
        var totalCount = await candidateIds.CountAsync(cancellationToken);
        var ids = await candidateIds
            .OrderBy(m => m.Name)
            .ThenBy(m => m.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return new PaginatedList<DiagnosticItemDto>([], totalCount, request.PageNumber, request.PageSize);

        var pageItems = await GetMediaIssuesAsync(request, cancellationToken, ids);
        pageItems = ApplyIssueFilters(pageItems, request);
        pageItems = pageItems
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.EntityName)
            .ThenBy(item => item.Issues[0])
            .ToList();
        return new PaginatedList<DiagnosticItemDto>(pageItems, totalCount, request.PageNumber, request.PageSize);
    }

    private static HashSet<DiagnosticIssue>? GetRequestedMediaCatalogIssues(GetDiagnosticItemsQuery request)
    {
        HashSet<DiagnosticIssue>? issues = null;

        if (request.Issue is { } single && IsMediaCatalogIssue(single))
            issues = [DiagnosticIssueTaxonomy.Canonicalize(single)];

        if (request.Issues is { Count: > 0 })
        {
            var fromList = request.Issues
                .Where(IsMediaCatalogIssue)
                .Select(DiagnosticIssueTaxonomy.Canonicalize)
                .ToHashSet();
            if (fromList.Count == 0)
                return [];

            issues = issues is null ? fromList : issues.Intersect(fromList).ToHashSet();
        }

        return issues;
    }

    private IQueryable<Guid> BuildBroadMediaCandidateIdQuery(
        IQueryable<MediaLibraryAvailability> availability,
        Guid? libraryId,
        IQueryable<Guid> duplicateExternalIdMediaIds,
        IQueryable<Guid> suspectedDuplicateMediaIds,
        IReadOnlyCollection<Guid> staleMediaIds)
    {
        // Tracks are linked via IndexedFiles, not MediaLibraryAvailability (albums are).
        // Union them explicitly so MissingAudioAnalysis matches the summary counts.
        var viaAvailability = _context.Medias
            .AsNoTracking()
            .Where(m => availability.Any(a => a.MediaId == m.Id))
            .Where(m => !m.ExternalIds.Any()
                        || m.MetadataTags.Count(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre) == 0
                        || _context.Medias.OfType<MusicTrack>().Any(t => t.Id == m.Id && t.AudioAnalysis == null)
                        || (m.Type == MediaType.Movie || m.Type == MediaType.Serie)
                            && (!_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Poster)
                                || !_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Backdrop))
                        || m.Type == MediaType.SerieSeason
                            && !_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Poster)
                        || m.Type == MediaType.SerieEpisode
                            && !_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Still)
                        || m.Type == MediaType.MusicAlbum
                            && !_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Cover)
                        || m.Type == MediaType.Serie
                            && _context.Medias.OfType<SerieEpisode>().Any(e =>
                                e.SerieId == m.Id
                                && _context.IndexedFiles.Any(f =>
                                    f.MediaId == e.Id
                                    && _context.Libraries.Any(l =>
                                        l.Id == f.LibraryId
                                        && l.IntroDetectionEnabled
                                        && l.ThemeSongGenerationEnabled)))
                        || m.Type == MediaType.SerieEpisode
                            && !_context.MediaSegments.Any(s =>
                                s.MediaId == m.Id
                                && (s.Type == MediaSegmentType.Intro || s.Type == MediaSegmentType.Outro))
                            && _context.IndexedFiles.Any(f =>
                                f.MediaId == m.Id
                                && _context.Libraries.Any(l =>
                                    l.Id == f.LibraryId && l.IntroDetectionEnabled))
                        || duplicateExternalIdMediaIds.Contains(m.Id)
                        || suspectedDuplicateMediaIds.Contains(m.Id)
                        || staleMediaIds.Contains(m.Id))
            .Select(m => m.Id);

        return viaAvailability.Union(QueryMusicTrackIdsMissingAudioAnalysis(libraryId));
    }

    private IQueryable<Guid> BuildFilteredMediaCandidateIdQuery(
        IQueryable<MediaLibraryAvailability> availability,
        Guid? libraryId,
        IReadOnlyCollection<DiagnosticIssue> activeIssues,
        IQueryable<Guid> duplicateExternalIdMediaIds,
        IQueryable<Guid> suspectedDuplicateMediaIds,
        IReadOnlyCollection<Guid> staleMediaIds)
    {
        var medias = _context.Medias
            .AsNoTracking()
            .Where(m => availability.Any(a => a.MediaId == m.Id));

        IQueryable<Guid>? union = null;
        void Add(IQueryable<Guid> next) => union = union is null ? next : union.Union(next);

        if (activeIssues.Contains(DiagnosticIssue.MissingExternalId))
        {
            Add(medias
                .Where(m => m.Type == MediaType.Movie || m.Type == MediaType.Serie || m.Type == MediaType.MusicAlbum)
                .Where(m => !m.ExternalIds.Any())
                .Select(m => m.Id));
        }

        if (activeIssues.Contains(DiagnosticIssue.MissingMetadata))
        {
            Add(medias
                .Where(m => m.MetadataTags.Count(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre) == 0)
                .Where(m => m.ExternalIds.Any()
                    || m.Type != MediaType.Movie && m.Type != MediaType.Serie && m.Type != MediaType.MusicAlbum)
                .Select(m => m.Id));
        }

        if (activeIssues.Contains(DiagnosticIssue.MissingPictures))
        {
            Add(medias.Where(m =>
                    (m.Type == MediaType.Movie || m.Type == MediaType.Serie)
                        && (!_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Poster)
                            || !_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Backdrop))
                    || m.Type == MediaType.SerieSeason
                        && !_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Poster)
                    || m.Type == MediaType.SerieEpisode
                        && !_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Still)
                    || m.Type == MediaType.MusicAlbum
                        && !_context.MetadataPictures.Any(p => p.MediaId == m.Id && p.Type == MetadataPictureType.Cover))
                .Select(m => m.Id));
        }

        if (activeIssues.Contains(DiagnosticIssue.MissingAudioAnalysis))
            Add(QueryMusicTrackIdsMissingAudioAnalysis(libraryId));

        if (activeIssues.Contains(DiagnosticIssue.StaleMetadata))
            Add(medias.Where(m => staleMediaIds.Contains(m.Id)).Select(m => m.Id));

        if (activeIssues.Contains(DiagnosticIssue.MissingThemeSong))
        {
            Add(medias
                .Where(m => m.Type == MediaType.Serie
                    && _context.Medias.OfType<SerieEpisode>().Any(e =>
                        e.SerieId == m.Id
                        && _context.IndexedFiles.Any(f =>
                            f.MediaId == e.Id
                            && _context.Libraries.Any(l =>
                                l.Id == f.LibraryId
                                && l.IntroDetectionEnabled
                                && l.ThemeSongGenerationEnabled))))
                .Select(m => m.Id));
        }

        if (activeIssues.Contains(DiagnosticIssue.MissingIntroOutro))
        {
            Add(medias
                .Where(m => m.Type == MediaType.SerieEpisode
                    && !_context.MediaSegments.Any(s =>
                        s.MediaId == m.Id
                        && (s.Type == MediaSegmentType.Intro || s.Type == MediaSegmentType.Outro))
                    && _context.IndexedFiles.Any(f =>
                        f.MediaId == m.Id
                        && _context.Libraries.Any(l =>
                            l.Id == f.LibraryId && l.IntroDetectionEnabled)))
                .Select(m => m.Id));
        }

        if (activeIssues.Contains(DiagnosticIssue.DuplicateExternalId))
            Add(duplicateExternalIdMediaIds.Where(id => availability.Any(a => a.MediaId == id)));

        if (activeIssues.Contains(DiagnosticIssue.SuspectedDuplicateMedia))
            Add(suspectedDuplicateMediaIds.Where(id => availability.Any(a => a.MediaId == id)));

        return union ?? medias.Where(_ => false).Select(m => m.Id);
    }

    private async Task<IReadOnlyCollection<Guid>> GetStaleRefreshableMediaIdsAsync(
        IQueryable<MediaLibraryAvailability> availability,
        CancellationToken cancellationToken)
    {
        var pairs = await availability
            .Join(
                _context.Libraries.AsNoTracking(),
                a => a.LibraryId,
                l => l.Id,
                (a, l) => new { a.MediaId, l.MetadataRefreshIntervalDays })
            .Where(x => x.MetadataRefreshIntervalDays != null && x.MetadataRefreshIntervalDays > 0)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (pairs.Count == 0)
            return [];

        var mediaIds = pairs.Select(p => p.MediaId).Distinct().ToList();
        var medias = await _context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Where(m => m.Type == MediaType.Movie
                || m.Type == MediaType.Serie
                || m.Type == MediaType.MusicAlbum
                || m.Type == MediaType.MusicArtist)
            .Select(m => new { m.Id, m.LastMetadataRefreshedAt })
            .ToListAsync(cancellationToken);

        var intervalByMedia = pairs
            .GroupBy(p => p.MediaId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.MetadataRefreshIntervalDays!.Value));

        var utcNow = DateTimeOffset.UtcNow;
        return medias
            .Where(m => intervalByMedia.TryGetValue(m.Id, out var days)
                && MetadataStalenessHelper.IsStale(m.LastMetadataRefreshedAt, days, utcNow))
            .Select(m => m.Id)
            .ToList();
    }

    private IQueryable<Guid> QueryMusicTrackIdsMissingAudioAnalysis(Guid? libraryId)
    {
        var tracks = _context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => t.AudioAnalysis == null);

        if (libraryId.HasValue)
        {
            return tracks
                .Where(t => _context.IndexedFiles.Any(f => f.MediaId == t.Id && f.LibraryId == libraryId.Value))
                .Select(t => t.Id);
        }

        return tracks
            .Where(t => _context.IndexedFiles.Any(f =>
                f.MediaId == t.Id
                && _context.Libraries.Any(l =>
                    l.Id == f.LibraryId
                    && l.MediaType == LibraryMediaType.Music
                    && l.PeerServerId == null)))
            .Select(t => t.Id);
    }

    private async Task<PaginatedList<DiagnosticItemDto>> GetMissingThemeSongIssuePageAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken)
    {
        var missingIds = await ThemeSongDiagnosticHelper.GetMissingThemeSerieIdsAsync(
            _context, _paths, request.LibraryId, limitToSerieIds: null, cancellationToken: cancellationToken);

        if (missingIds.Count == 0)
            return EmptyPage(request);

        var ordered = await _context.Medias
            .OfType<Serie>()
            .AsNoTracking()
            .Where(s => missingIds.Contains(s.Id))
            .OrderBy(s => s.Title ?? "(untitled)")
            .ThenBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var totalCount = ordered.Count;
        var pageIds = ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        if (pageIds.Count == 0)
            return new PaginatedList<DiagnosticItemDto>([], totalCount, request.PageNumber, request.PageSize);

        var items = await GetMediaIssuesAsync(request, cancellationToken, pageIds);
        items = ApplyIssueFilters(items, request);
        return new PaginatedList<DiagnosticItemDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<PaginatedList<DiagnosticItemDto>> GetMissingIntroOutroIssuePageAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken)
    {
        var missing = await IntroOutroDiagnosticHelper.GetMissingIntroOutroEpisodesAsync(
            _context, request.LibraryId, limitToEpisodeIds: null, cancellationToken: cancellationToken);

        if (missing.Count == 0)
            return EmptyPage(request);

        var missingIds = missing.Select(c => c.EpisodeId).ToHashSet();
        var ordered = await _context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => missingIds.Contains(e.Id))
            .OrderBy(e => e.Title ?? "(untitled)")
            .ThenBy(e => e.Id)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var totalCount = ordered.Count;
        var pageIds = ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        if (pageIds.Count == 0)
            return new PaginatedList<DiagnosticItemDto>([], totalCount, request.PageNumber, request.PageSize);

        var items = await GetMediaIssuesAsync(request, cancellationToken, pageIds);
        items = ApplyIssueFilters(items, request);
        return new PaginatedList<DiagnosticItemDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private static PaginatedList<DiagnosticItemDto> EmptyPage(GetDiagnosticItemsQuery request) =>
        new([], 0, request.PageNumber, request.PageSize);

    private static bool IsOnlyIssueFilter(GetDiagnosticItemsQuery request, DiagnosticIssue issue) =>
        request.Issue == issue
        || request.Issues is { Count: > 0 } && request.Issues.All(i => i == issue);

    private static bool IsMediaCatalogIssue(DiagnosticIssue issue) =>
        DiagnosticIssueTaxonomy.Canonicalize(issue) is
            DiagnosticIssue.MissingPictures
            or DiagnosticIssue.MissingExternalId
            or DiagnosticIssue.MissingMetadata
            or DiagnosticIssue.StaleMetadata
            or DiagnosticIssue.MissingAudioAnalysis
            or DiagnosticIssue.MissingThemeSong
            or DiagnosticIssue.MissingIntroOutro
            or DiagnosticIssue.DuplicateExternalId
            or DiagnosticIssue.SuspectedDuplicateMedia;

    private static IReadOnlyList<MetadataPictureType> GetExpectedPictureTypes(MediaType type) => type switch
    {
        MediaType.Movie => [MetadataPictureType.Poster, MetadataPictureType.Backdrop],
        MediaType.Serie => [MetadataPictureType.Poster, MetadataPictureType.Backdrop],
        MediaType.SerieSeason => [MetadataPictureType.Poster],
        MediaType.SerieEpisode => [MetadataPictureType.Still],
        MediaType.MusicAlbum => [MetadataPictureType.Cover],
        _ => []
    };

    private sealed class AlbumDiagnosticInfo
    {
        public required Guid Id { get; init; }
        public required bool HasExternalIds { get; init; }
        public required int GenreCount { get; init; }
    }

    private sealed class IndexedFileIssueRow
    {
        public required Guid EntityId { get; init; }
        public required string EntityName { get; init; }
        public required string Path { get; init; }
        public required Guid LibraryId { get; init; }
        public required string LibraryTitle { get; init; }
        public required DiagnosticIssue Issue { get; init; }
        public required DiagnosticSeverity Severity { get; init; }
    }

    private async Task<List<DiagnosticItemDto>> GetMusicArtistIssuesAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? selectedArtistIds = null)
    {
        var query = _context.Medias.OfType<MusicArtist>()
            .AsNoTracking()
            .AsQueryable();

        if (selectedArtistIds is not null)
            query = query.Where(a => selectedArtistIds.Contains(a.Id));

        var artists = await query
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.LastMetadataRefreshedAt,
                HasMembers = a.PersonRoles.Any(),
                HasExternalIds = a.ExternalIds.Any()
            })
            .ToListAsync(cancellationToken);

        // Find the library via the artist's albums
        var artistIds = artists.Select(a => a.Id).ToHashSet();
        var artistLibraryPairs = await (
            from album in _context.Medias.OfType<MusicAlbum>()
            where album.ArtistId != null && artistIds.Contains(album.ArtistId.Value)
            join f in _context.IndexedFiles on album.Id equals f.MediaId
            select new { ArtistId = album.ArtistId!.Value, f.LibraryId }
        ).Distinct().ToListAsync(cancellationToken);

        var artistToLibrary = artistLibraryPairs
            .DistinctBy(x => x.ArtistId)
            .ToDictionary(x => x.ArtistId, x => x.LibraryId);

        if (request.LibraryId.HasValue)
        {
            artists = artists.Where(a => artistToLibrary.GetValueOrDefault(a.Id) == request.LibraryId.Value).ToList();
        }

        var libraryIds = artistToLibrary.Values.Distinct().ToList();
        var libraryInfo = await _context.Libraries
            .Where(l => libraryIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Title, cancellationToken);

        return artists.Select(a =>
        {
            var issues = new List<DiagnosticIssue>();
            if (!a.HasMembers) issues.Add(DiagnosticIssue.MissingMembers);
            if (!a.HasExternalIds) issues.Add(DiagnosticIssue.MissingMetadata);

            if (issues.Count == 0) return null;

            var libraryId = artistToLibrary.GetValueOrDefault(a.Id);
            var severity = issues.Max(GetIssueSeverity);

            return new DiagnosticItemDto
            {
                EntityId = a.Id,
                EntityName = a.Title ?? "(untitled)",
                EntityType = DiagnosticEntityType.Media,
                LibraryId = libraryId,
                LibraryTitle = libraryInfo.GetValueOrDefault(libraryId, ""),
                Issues = issues,
                Severity = severity,
                MediaType = MediaType.MusicArtist,
                MediaUrl = MediaPageUrls.Build(MediaType.MusicArtist, a.Id),
                LastMetadataRefreshedAt = a.LastMetadataRefreshedAt
            };
        })
        .Where(dto => dto is not null)
        .Cast<DiagnosticItemDto>()
        .ToList();
    }

    private async Task<PaginatedList<DiagnosticItemDto>> GetMusicArtistIssuePageAsync(
        GetDiagnosticItemsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Issue is { } singleIssue
            && singleIssue is not (DiagnosticIssue.MissingMembers or DiagnosticIssue.MissingMetadata))
        {
            return EmptyPage(request);
        }

        if (request.Issues is { Count: > 0 }
            && !request.Issues.Any(i => i is DiagnosticIssue.MissingMembers or DiagnosticIssue.MissingMetadata))
        {
            return EmptyPage(request);
        }

        var artists = _context.Medias.OfType<MusicArtist>()
            .AsNoTracking()
            .Where(a => !a.PersonRoles.Any() || !a.ExternalIds.Any());

        if (request.LibraryId.HasValue)
        {
            artists = artists.Where(a => _context.Medias.OfType<MusicAlbum>()
                .Where(album => album.ArtistId == a.Id)
                .Join(_context.IndexedFiles, album => album.Id, file => file.MediaId, (_, file) => file.LibraryId)
                .Any(libraryId => libraryId == request.LibraryId.Value));
        }

        var orderedArtists = artists
            .OrderBy(a => a.Title ?? "(untitled)")
            .ThenBy(a => a.Id);

        // Always page by artist id; multiple issues stay on the same row.
        var totalCount = await orderedArtists.CountAsync(cancellationToken);
        var ids = await orderedArtists
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var pageItems = await GetMusicArtistIssuesAsync(request, cancellationToken, ids);
        pageItems = ApplyIssueFilters(pageItems, request);
        pageItems = pageItems
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.EntityName)
            .ThenBy(item => item.Issues[0])
            .ToList();
        return new PaginatedList<DiagnosticItemDto>(pageItems, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<List<DiagnosticItemDto>> GetScanIssuesAsync(GetDiagnosticItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ScanIssues
            .AsNoTracking()
            .Where(s => !_context.Libraries.Any(l => l.Id == s.LibraryId && l.PeerServerId != null))
            .AsQueryable();

        if (request.LibraryId.HasValue)
        {
            query = query.Where(s => s.LibraryId == request.LibraryId.Value);
        }

        var issues = await query
            .Select(s => new
            {
                s.Id,
                s.Path,
                s.ErrorMessage,
                s.LibraryId,
                LibraryTitle = _context.Libraries.Where(l => l.Id == s.LibraryId).Select(l => l.Title).FirstOrDefault() ?? ""
            })
            .ToListAsync(cancellationToken);

        return issues.Select(s => new DiagnosticItemDto
        {
            EntityId = s.Id,
            EntityName = s.Path,
            EntityType = DiagnosticEntityType.Library,
            LibraryId = s.LibraryId,
            LibraryTitle = s.LibraryTitle,
            Issues = [DiagnosticIssue.InaccessiblePath],
            Severity = DiagnosticSeverity.Error,
            DetailText = s.ErrorMessage
        }).ToList();
    }
}
