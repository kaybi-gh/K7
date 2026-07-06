using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Diagnostics;
using K7.Shared.Navigation;

namespace K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticItems;

[Authorize(Roles = Roles.Administrator)]
public record GetDiagnosticItemsQuery : IRequest<PaginatedList<DiagnosticItemDto>>
{
    public Guid? LibraryId { get; init; }
    public DiagnosticEntityType? EntityType { get; init; }
    public DiagnosticIssue? Issue { get; init; }
    public IReadOnlyCollection<DiagnosticIssue>? Issues { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 20;
}

public class GetDiagnosticItemsQueryHandler : IRequestHandler<GetDiagnosticItemsQuery, PaginatedList<DiagnosticItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDiagnosticItemsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<DiagnosticItemDto>> Handle(GetDiagnosticItemsQuery request, CancellationToken cancellationToken)
    {
        var items = new List<DiagnosticItemDto>();

        if (request.EntityType is null or DiagnosticEntityType.IndexedFile)
        {
            items.AddRange(await GetIndexedFileIssuesAsync(request, cancellationToken));
        }

        if (request.EntityType is null or DiagnosticEntityType.Media)
        {
            items.AddRange(await GetMediaIssuesAsync(request, cancellationToken));
            items.AddRange(await GetMusicArtistIssuesAsync(request, cancellationToken));
        }

        if (request.EntityType is null or DiagnosticEntityType.Library)
        {
            items.AddRange(await GetScanIssuesAsync(request, cancellationToken));
        }

        items = ExpandToOneRowPerIssue(items);

        if (request.Issue.HasValue)
        {
            items = items.Where(i => i.Issues.Contains(request.Issue.Value)).ToList();
        }

        if (request.Issues is { Count: > 0 })
        {
            items = items.Where(i => i.Issues.Any(iss => request.Issues.Contains(iss))).ToList();
        }

        items = [.. items.OrderByDescending(i => i.Severity).ThenBy(i => i.EntityName).ThenBy(i => i.Issues[0])];

        var totalCount = items.Count;
        var paged = items
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PaginatedList<DiagnosticItemDto>(paged, totalCount, request.PageNumber, request.PageSize);
    }

    private static List<DiagnosticItemDto> ExpandToOneRowPerIssue(IEnumerable<DiagnosticItemDto> items) =>
        items.SelectMany(item => item.Issues.Select(issue => item with
        {
            Issues = [issue],
            Severity = GetIssueSeverity(issue)
        })).ToList();

    private static DiagnosticSeverity GetIssueSeverity(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile or DiagnosticIssue.MissingFiles or DiagnosticIssue.MissingFileMetadata
            => DiagnosticSeverity.Error,
        DiagnosticIssue.StaleMetadata or DiagnosticIssue.MissingAudioAnalysis or DiagnosticIssue.MissingMembers
            => DiagnosticSeverity.Info,
        _ => DiagnosticSeverity.Warning
    };

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
                f.LibraryId,
                LibraryTitle = _context.Libraries.Where(l => l.Id == f.LibraryId).Select(l => l.Title).FirstOrDefault() ?? "",
                IsOrphan = f.MediaId == null,
                IsUnidentified = f.Identification == null,
                HasNoFileMetadata = f.FileMetadata == null,
                HasNoHlsSegments = f.FileMetadata != null
                    && f.FileMetadata.Type == FileType.Video
                    && _context.Libraries.Any(l => l.Id == f.LibraryId && l.TransmuxingEnabled)
                    && !_context.HlsSegments.Any(s => s.IndexedFileId == f.Id)
            })
            .Where(f => f.IsOrphan || f.IsUnidentified || f.HasNoFileMetadata || f.HasNoHlsSegments)
            .ToListAsync(cancellationToken);

        return files.Select(f =>
        {
            var issues = new List<DiagnosticIssue>();
            if (f.IsOrphan) issues.Add(DiagnosticIssue.OrphanFile);
            if (f.IsUnidentified) issues.Add(DiagnosticIssue.UnidentifiedFile);
            if (f.HasNoFileMetadata) issues.Add(DiagnosticIssue.MissingFileMetadata);
            if (f.HasNoHlsSegments) issues.Add(DiagnosticIssue.MissingHlsSegments);

            var severity = issues.Contains(DiagnosticIssue.OrphanFile) || issues.Contains(DiagnosticIssue.MissingFileMetadata)
                ? DiagnosticSeverity.Error
                : DiagnosticSeverity.Warning;

            return new DiagnosticItemDto
            {
                EntityId = f.Id,
                EntityName = f.Name,
                EntityType = DiagnosticEntityType.IndexedFile,
                LibraryId = f.LibraryId,
                LibraryTitle = f.LibraryTitle,
                Issues = issues,
                Severity = severity
            };
        }).ToList();
    }

    private async Task<List<DiagnosticItemDto>> GetMediaIssuesAsync(GetDiagnosticItemsQuery request, CancellationToken cancellationToken)
    {
        var libraryMediaQuery = _context.IndexedFiles
            .Where(f => f.MediaId != null)
            .Where(f => !_context.Libraries.Any(l => l.Id == f.LibraryId && l.PeerServerId != null))
            .AsQueryable();

        if (request.LibraryId.HasValue)
        {
            libraryMediaQuery = libraryMediaQuery.Where(f => f.LibraryId == request.LibraryId.Value);
        }

        var libraryMediaPairs = await libraryMediaQuery
            .Select(f => new { f.MediaId, f.LibraryId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var mediaToLibrary = libraryMediaPairs
            .DistinctBy(x => x.MediaId)
            .ToDictionary(x => x.MediaId!.Value, x => x.LibraryId);

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
                HasIndexedFiles = m.IndexedFiles.Any(),
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

            if (!m.HasIndexedFiles) issues.Add(DiagnosticIssue.MissingFiles);

            var isStale = MetadataStalenessHelper.IsStale(
                m.LastMetadataRefreshedAt, threshold, DateTimeOffset.UtcNow);
            if (isStale) issues.Add(DiagnosticIssue.StaleMetadata);

            if (m.IsMusicTrack && !m.HasAudioAnalysis) issues.Add(DiagnosticIssue.MissingAudioAnalysis);

            if (issues.Count == 0) return null;

            episodeNavById.TryGetValue(m.Id, out var episodeNav);
            seasonNavById.TryGetValue(m.Id, out var seasonNav);
            trackNavById.TryGetValue(m.Id, out var trackNav);

            var severity = issues.Contains(DiagnosticIssue.MissingFiles)
                ? DiagnosticSeverity.Error
                : issues.Contains(DiagnosticIssue.MissingPictures) || issues.Contains(DiagnosticIssue.MissingMetadata)
                    || issues.Contains(DiagnosticIssue.MissingExternalId)
                    ? DiagnosticSeverity.Warning
                    : DiagnosticSeverity.Info;

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

    private async Task<List<DiagnosticItemDto>> GetMusicArtistIssuesAsync(GetDiagnosticItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Medias.OfType<MusicArtist>()
            .AsNoTracking()
            .AsQueryable();

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

            return new DiagnosticItemDto
            {
                EntityId = a.Id,
                EntityName = a.Title ?? "(untitled)",
                EntityType = DiagnosticEntityType.Media,
                LibraryId = libraryId,
                LibraryTitle = libraryInfo.GetValueOrDefault(libraryId, ""),
                Issues = issues,
                Severity = DiagnosticSeverity.Warning,
                MediaType = MediaType.MusicArtist,
                MediaUrl = MediaPageUrls.Build(MediaType.MusicArtist, a.Id),
                LastMetadataRefreshedAt = a.LastMetadataRefreshedAt
            };
        })
        .Where(dto => dto is not null)
        .Cast<DiagnosticItemDto>()
        .ToList();
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
            Severity = DiagnosticSeverity.Warning,
            DetailText = s.ErrorMessage
        }).ToList();
    }
}
