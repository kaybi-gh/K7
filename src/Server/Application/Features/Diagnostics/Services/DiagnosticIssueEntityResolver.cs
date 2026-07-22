using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Diagnostics.Services;

public class DiagnosticIssueEntityResolver(
    IApplicationDbContext context,
    IOptions<PathsConfiguration> pathsOptions)
{
    private readonly PathsConfiguration _paths = pathsOptions.Value;

    public async Task<List<Guid>> ResolveEntityIdsAsync(
        DiagnosticIssue issue,
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        return issue switch
        {
            DiagnosticIssue.MissingFileMetadata => await GetIndexedFileIdsMissingFileMetadataAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingHlsSegments => await GetIndexedFileIdsMissingHlsSegmentsAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingChapters => await GetIndexedFileIdsMissingChaptersAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingAudioAnalysis => await GetMusicTrackIdsMissingAudioAnalysisAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingPictures => await GetMediaIdsMissingPicturesAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingMetadata => await GetMediaIdsMissingMetadataAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingExternalId => await GetMediaIdsMissingExternalIdAsync(libraryId, cancellationToken),
            DiagnosticIssue.StaleMetadata => await GetMediaIdsWithStaleMetadataAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingMembers => await GetMusicArtistIdsMissingMembersAsync(libraryId, cancellationToken),
            DiagnosticIssue.OrphanFile => await GetOrphanIndexedFileIdsAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingThemeSong => await GetSerieIdsMissingThemeSongAsync(libraryId, cancellationToken),
            DiagnosticIssue.MissingIntroOutro => await GetEpisodeIdsMissingIntroOutroAsync(libraryId, cancellationToken),
            _ => []
        };
    }

    private async Task<List<Guid>> GetSerieIdsMissingThemeSongAsync(Guid? libraryId, CancellationToken cancellationToken)
    {
        var ids = await ThemeSongDiagnosticHelper.GetMissingThemeSerieIdsAsync(
            context, _paths, libraryId, limitToSerieIds: null, cancellationToken);
        return ids.ToList();
    }

    private async Task<List<Guid>> GetEpisodeIdsMissingIntroOutroAsync(Guid? libraryId, CancellationToken cancellationToken)
    {
        var ids = await IntroOutroDiagnosticHelper.GetMissingIntroOutroEpisodeIdsAsync(
            context, libraryId, limitToEpisodeIds: null, cancellationToken);
        return ids.ToList();
    }

    private IQueryable<Guid> NonFederatedLibraryIds() =>
        context.Libraries
            .Where(l => l.PeerServerId == null)
            .Select(l => l.Id);

    private async Task<List<Guid>> GetIndexedFileIdsMissingFileMetadataAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var query = context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.FileMetadata == null && NonFederatedLibraryIds().Contains(f.LibraryId));

        if (libraryId.HasValue)
            query = query.Where(f => f.LibraryId == libraryId.Value);

        return await query.Select(f => f.Id).ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetIndexedFileIdsMissingHlsSegmentsAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var query = context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.FileMetadata != null
                && f.FileMetadata.Type == FileType.Video
                && context.Libraries.Any(l => l.Id == f.LibraryId && l.TransmuxingEnabled)
                && !context.HlsSegments.Any(s => s.IndexedFileId == f.Id)
                && NonFederatedLibraryIds().Contains(f.LibraryId));

        if (libraryId.HasValue)
            query = query.Where(f => f.LibraryId == libraryId.Value);

        return await query.Select(f => f.Id).ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetIndexedFileIdsMissingChaptersAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var query = context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.FileMetadata != null
                && f.FileMetadata.Type == FileType.Video
                && context.Libraries.Any(l => l.Id == f.LibraryId && l.ChapterExtractionEnabled)
                && context.FileMetadatas.OfType<VideoFileMetadata>()
                    .Any(m => m.Id == f.FileMetadata!.Id && m.Chapters == null)
                && NonFederatedLibraryIds().Contains(f.LibraryId));

        if (libraryId.HasValue)
            query = query.Where(f => f.LibraryId == libraryId.Value);

        return await query.Select(f => f.Id).ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetMusicTrackIdsMissingAudioAnalysisAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var query = context.Medias
            .AsNoTracking()
            .OfType<MusicTrack>()
            .Where(t => t.AudioAnalysis == null);

        if (libraryId.HasValue)
        {
            query = query.Where(t => context.IndexedFiles
                .Any(f => f.MediaId == t.Id && f.LibraryId == libraryId.Value));
        }
        else
        {
            query = query.Where(t => context.IndexedFiles.Any(f =>
                f.MediaId == t.Id
                && context.Libraries.Any(l =>
                    l.Id == f.LibraryId
                    && l.MediaType == LibraryMediaType.Music
                    && l.MusicAudioAnalysisEnabled
                    && l.PeerServerId == null)));
        }

        return await query.Select(t => t.Id).ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, Guid>> GetMediaToLibraryMapAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var query = context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId != null && NonFederatedLibraryIds().Contains(f.LibraryId));

        if (libraryId.HasValue)
            query = query.Where(f => f.LibraryId == libraryId.Value);

        var pairs = await query
            .Select(f => new { MediaId = f.MediaId!.Value, f.LibraryId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return pairs
            .DistinctBy(x => x.MediaId)
            .ToDictionary(x => x.MediaId, x => x.LibraryId);
    }

    private async Task<List<Guid>> GetMediaIdsMissingPicturesAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var mediaToLibrary = await GetMediaToLibraryMapAsync(libraryId, cancellationToken);
        if (mediaToLibrary.Count == 0)
            return [];

        var mediaIds = mediaToLibrary.Keys.ToHashSet();
        var medias = await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Type })
            .ToListAsync(cancellationToken);

        var pictureTypes = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.MediaId != null && mediaIds.Contains(p.MediaId.Value))
            .Select(p => new { Id = p.MediaId!.Value, p.Type })
            .ToListAsync(cancellationToken);

        var picturesByMedia = pictureTypes
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Type).ToHashSet());

        return medias
            .Where(m => GetExpectedPictureTypes(m.Type)
                .Except(picturesByMedia.GetValueOrDefault(m.Id, []))
                .Any())
            .Select(m => m.Id)
            .ToList();
    }

    private async Task<List<Guid>> GetMediaIdsMissingMetadataAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var mediaToLibrary = await GetMediaToLibraryMapAsync(libraryId, cancellationToken);
        if (mediaToLibrary.Count == 0)
            return [];

        var mediaIds = mediaToLibrary.Keys.ToHashSet();
        var medias = await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new
            {
                m.Id,
                m.Type,
                HasExternalIds = m.ExternalIds.Any(),
                GenreCount = m.MetadataTags.Count(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            })
            .ToListAsync(cancellationToken);

        return medias
            .Where(m =>
            {
                var isEnrichableMedia = m.Type is MediaType.Movie or MediaType.Serie or MediaType.MusicAlbum;
                if (!m.HasExternalIds && !isEnrichableMedia && m.GenreCount == 0)
                    return true;
                return m.HasExternalIds && m.GenreCount == 0;
            })
            .Select(m => m.Id)
            .ToList();
    }

    private async Task<List<Guid>> GetMediaIdsMissingExternalIdAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var mediaToLibrary = await GetMediaToLibraryMapAsync(libraryId, cancellationToken);
        if (mediaToLibrary.Count == 0)
            return [];

        var mediaIds = mediaToLibrary.Keys.ToHashSet();
        return await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Where(m => m.Type == MediaType.Movie || m.Type == MediaType.Serie || m.Type == MediaType.MusicAlbum)
            .Where(m => !m.ExternalIds.Any())
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetMediaIdsWithStaleMetadataAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var mediaToLibrary = await GetMediaToLibraryMapAsync(libraryId, cancellationToken);
        if (mediaToLibrary.Count == 0)
            return [];

        var libraryIds = mediaToLibrary.Values.Distinct().ToList();
        var libraryInfo = await context.Libraries
            .AsNoTracking()
            .Where(l => libraryIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.MetadataRefreshIntervalDays, cancellationToken);

        var mediaIds = mediaToLibrary.Keys.ToHashSet();
        var medias = await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.LastMetadataRefreshedAt })
            .ToListAsync(cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;
        return medias
            .Where(m =>
            {
                var threshold = libraryInfo.GetValueOrDefault(mediaToLibrary[m.Id]);
                return MetadataStalenessHelper.IsStale(m.LastMetadataRefreshedAt, threshold, utcNow);
            })
            .Select(m => m.Id)
            .ToList();
    }

    private async Task<List<Guid>> GetMusicArtistIdsMissingMembersAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var artists = await context.Medias
            .AsNoTracking()
            .OfType<MusicArtist>()
            .Where(a => !a.PersonRoles.Any())
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (!libraryId.HasValue)
            return artists;

        var artistIdsInLibrary = await (
            from album in context.Medias.OfType<MusicAlbum>()
            where album.ArtistId != null && artists.Contains(album.ArtistId.Value)
            join f in context.IndexedFiles on album.Id equals f.MediaId
            where f.LibraryId == libraryId.Value
            select album.ArtistId!.Value
        ).Distinct().ToListAsync(cancellationToken);

        return artistIdsInLibrary;
    }

    private async Task<List<Guid>> GetOrphanIndexedFileIdsAsync(
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        var query = context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId == null && f.Identification != null && NonFederatedLibraryIds().Contains(f.LibraryId));

        if (libraryId.HasValue)
            query = query.Where(f => f.LibraryId == libraryId.Value);

        return await query.Select(f => f.Id).ToListAsync(cancellationToken);
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
}
