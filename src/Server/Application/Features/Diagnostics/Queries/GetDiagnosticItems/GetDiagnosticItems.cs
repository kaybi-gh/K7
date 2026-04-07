using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Diagnostics;

namespace K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticItems;

[Authorize(Roles = Roles.Administrator)]
public record GetDiagnosticItemsQuery : IRequest<PaginatedList<DiagnosticItemDto>>
{
    public Guid? LibraryId { get; init; }
    public DiagnosticEntityType? EntityType { get; init; }
    public DiagnosticIssue? Issue { get; init; }
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
        }

        if (request.Issue.HasValue)
        {
            items = items.Where(i => i.Issues.Contains(request.Issue.Value)).ToList();
        }

        items = [.. items.OrderByDescending(i => i.Severity).ThenBy(i => i.EntityName)];

        var totalCount = items.Count;
        var paged = items
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PaginatedList<DiagnosticItemDto>(paged, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<List<DiagnosticItemDto>> GetIndexedFileIssuesAsync(GetDiagnosticItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.IndexedFiles
            .Include(f => f.FileMetadata)
                .ThenInclude(fm => fm!.HlsSegments)
            .AsNoTracking()
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
                HasNoHlsSegments = f.FileMetadata != null && !f.FileMetadata.HlsSegments.Any()
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
            .AsQueryable();

        if (request.LibraryId.HasValue)
        {
            libraryMediaQuery = libraryMediaQuery.Where(f => f.LibraryId == request.LibraryId.Value);
        }

        var libraryMediaMap = await libraryMediaQuery
            .GroupBy(f => f.MediaId!.Value)
            .Select(g => new
            {
                MediaId = g.Key,
                LibraryId = g.First().LibraryId
            })
            .ToListAsync(cancellationToken);

        var mediaIds = libraryMediaMap.Select(x => x.MediaId).ToHashSet();
        var mediaToLibrary = libraryMediaMap.ToDictionary(x => x.MediaId, x => x.LibraryId);

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
                PictureTypes = m.Pictures.Select(p => p.Type).Distinct().ToList(),
                HasExternalIds = m.ExternalIds.Any(),
                GenreCount = m.Genres.Count,
                HasIndexedFiles = m.IndexedFiles.Any(),
                IsMusicTrack = m is MusicTrack,
                HasAudioAnalysis = m is MusicTrack && ((MusicTrack)m).AudioAnalysis != null
            })
            .ToListAsync(cancellationToken);

        return medias.Select(m =>
        {
            var libraryId = mediaToLibrary.GetValueOrDefault(m.Id);
            var libInfo = libraryInfo.GetValueOrDefault(libraryId);
            var threshold = libInfo?.MetadataRefreshIntervalDays;

            var expectedPictures = GetExpectedPictureTypes(m.Type);
            var missingPictures = expectedPictures.Except(m.PictureTypes).ToList();

            var issues = new List<DiagnosticIssue>();
            if (missingPictures.Count > 0) issues.Add(DiagnosticIssue.MissingPictures);
            if (!m.HasExternalIds && m.GenreCount == 0) issues.Add(DiagnosticIssue.MissingMetadata);
            if (!m.HasIndexedFiles) issues.Add(DiagnosticIssue.MissingFiles);

            var isStale = m.LastMetadataRefreshedAt is null
                || (threshold.HasValue && m.LastMetadataRefreshedAt < DateTimeOffset.UtcNow.AddDays(-threshold.Value));
            if (isStale) issues.Add(DiagnosticIssue.StaleMetadata);

            if (m.IsMusicTrack && !m.HasAudioAnalysis) issues.Add(DiagnosticIssue.MissingAudioAnalysis);

            if (issues.Count == 0) return null;

            var severity = issues.Contains(DiagnosticIssue.MissingFiles)
                ? DiagnosticSeverity.Error
                : issues.Contains(DiagnosticIssue.MissingPictures) || issues.Contains(DiagnosticIssue.MissingMetadata)
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
                MediaUrl = BuildMediaUrl(m.Type, m.Id),
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
        MediaType.MusicAlbum => [MetadataPictureType.Poster],
        _ => []
    };

    private static string? BuildMediaUrl(MediaType type, Guid id) => type switch
    {
        MediaType.Movie => $"/movies/{id}",
        MediaType.Serie => $"/series/{id}",
        MediaType.MusicAlbum => $"/music/albums/{id}",
        _ => null
    };
}
