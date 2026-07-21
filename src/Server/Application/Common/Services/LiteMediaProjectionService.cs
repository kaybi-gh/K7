using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Common.Services;

public sealed class LiteMediaProjectionService(IApplicationDbContext context)
{
    private sealed record BaseRow(Guid Id, MediaType Type, string? Title, string? SortTitle, DateOnly? ReleaseDate, DateTimeOffset? Created);
    private sealed record PictureRow(Guid Id, Guid MediaId, MetadataPictureType Type, bool IsLocal, string? DominantColor, int? OriginalWidth, int? OriginalHeight);
    private sealed record AlbumRow(Guid Id, Guid? ArtistId, string? ArtistName);
    private sealed record TrackRow(Guid Id, Guid AlbumId, int? TrackNumber, Guid? ArtistId, string? AlbumTitle, Guid? AlbumArtistId, string? AlbumArtistName, string? ArtistName, double? LoudnessLufs, double? FadeInDuration, double? FadeOutDuration, double? ReplayGainTrackGain, string? Genre);
    private sealed record EpisodeRow(Guid Id, int EpisodeNumber, int SeasonNumber, Guid SerieId, string? SerieTitle, DateOnly? SerieReleaseDate, Guid SeasonId, string? Overview);
    private sealed record SeasonRow(Guid Id, Guid SerieId, int SeasonNumber, string? SerieTitle, int EpisodeCount);
    private sealed record ArtistRow(Guid Id, MusicArtistType ArtistType, string? Country);
    private sealed record FileRow(Guid MediaId, Guid Id, double? Duration);

    public async Task<IReadOnlyDictionary<Guid, int>> GetSerieSeasonCountsAsync(
        IEnumerable<BaseMedia> medias,
        CancellationToken cancellationToken = default) =>
        await SerieSeasonCountHelper.GetCountsBySerieIdsAsync(
            context,
            SerieSeasonCountHelper.ExtractSerieIdsFromMedias(medias),
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>> GetPictureSizesAsync(
        IEnumerable<BaseMedia> medias,
        CancellationToken cancellationToken = default) =>
        await MetadataPictureSizesHelper.GetAvailableSizesByPictureIdsAsync(
            context,
            MetadataPictureSizesHelper.ExtractPictureIdsFromMedias(medias),
            cancellationToken);

    public LiteMediaDto ToLite(
        BaseMedia media,
        IReadOnlyDictionary<Guid, int>? serieSeasonCounts = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null) =>
        media.ToLiteMediaDto(serieSeasonCounts, pictureSizes);

    public async Task<List<LiteMediaDto>> ToLiteListAsync(
        IEnumerable<BaseMedia> medias,
        CancellationToken cancellationToken = default)
    {
        var list = medias.ToList();
        var counts = await GetSerieSeasonCountsAsync(list, cancellationToken);
        var pictureSizes = await GetPictureSizesAsync(list, cancellationToken);
        return list.Select(m => m.ToLiteMediaDto(counts, pictureSizes)).ToList();
    }

    public async Task<List<LiteMediaDto>> GetLiteMediaDtosAsync(
        IReadOnlyList<Guid> mediaIds,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (mediaIds.Count == 0)
            return [];

        var idSet = mediaIds.ToHashSet();
        var baseRows = await context.Medias
            .AsNoTracking()
            .Where(m => idSet.Contains(m.Id))
            .Select(m => new BaseRow(m.Id, m.Type, m.Title, m.SortTitle, m.ReleaseDate, m.Created))
            .ToListAsync(cancellationToken);
        var baseById = baseRows.ToDictionary(m => m.Id);

        if (baseRows.Count == 0)
            return [];

        var trackRows = await context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => idSet.Contains(t.Id))
            .Select(t => new TrackRow(
                t.Id,
                t.AlbumId,
                t.TrackNumber,
                t.ArtistId,
                t.Album.Title,
                t.Album.ArtistId,
                t.Album.Artist != null ? t.Album.Artist.Title : null,
                t.Artist != null ? t.Artist.Title : null,
                t.AudioAnalysis != null ? t.AudioAnalysis.LoudnessLufs : null,
                t.AudioAnalysis != null ? t.AudioAnalysis.FadeInDuration : null,
                t.AudioAnalysis != null ? t.AudioAnalysis.FadeOutDuration : null,
                t.AudioAnalysis != null ? t.AudioAnalysis.ReplayGainTrackGain : null,
                t.MetadataTags
                    .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                    .Select(mt => mt.MetadataTag.DisplayName)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        var trackById = trackRows.ToDictionary(t => t.Id);

        var albumIdsInRequest = baseRows.Where(r => r.Type == MediaType.MusicAlbum).Select(r => r.Id).ToHashSet();
        var albumRows = albumIdsInRequest.Count == 0
            ? []
            : await context.Medias
                .OfType<MusicAlbum>()
                .AsNoTracking()
                .Where(a => albumIdsInRequest.Contains(a.Id))
                .Select(a => new AlbumRow(
                    a.Id,
                    a.ArtistId,
                    a.Artist != null ? a.Artist.Title : null))
                .ToListAsync(cancellationToken);
        var albumById = albumRows.ToDictionary(a => a.Id);

        var episodeRows = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => idSet.Contains(e.Id))
            .Select(e => new EpisodeRow(
                e.Id,
                e.EpisodeNumber,
                e.Season.SeasonNumber,
                e.SerieId,
                e.Serie.Title,
                e.Serie.ReleaseDate,
                e.SeasonId,
                e.Overview))
            .ToListAsync(cancellationToken);
        var episodeById = episodeRows.ToDictionary(e => e.Id);

        var seasonRows = await context.Medias
            .OfType<SerieSeason>()
            .AsNoTracking()
            .Where(s => idSet.Contains(s.Id))
            .Select(s => new SeasonRow(s.Id, s.SerieId, s.SeasonNumber, s.Serie.Title, s.Episodes.Count))
            .ToListAsync(cancellationToken);
        var seasonById = seasonRows.ToDictionary(s => s.Id);

        var artistRows = await context.Medias
            .OfType<MusicArtist>()
            .AsNoTracking()
            .Where(a => idSet.Contains(a.Id))
            .Select(a => new ArtistRow(a.Id, a.ArtistType, a.Country))
            .ToListAsync(cancellationToken);
        var artistById = artistRows.ToDictionary(a => a.Id);

        var artistIds = artistRows.Select(a => a.Id).ToHashSet();
        var artistAlbumRows = artistIds.Count == 0
            ? []
            : await context.Medias
                .OfType<MusicAlbum>()
                .AsNoTracking()
                .Where(a => a.ArtistId.HasValue && artistIds.Contains(a.ArtistId.Value))
                .Select(a => new { a.Id, a.ArtistId })
                .ToListAsync(cancellationToken);
        var guestAlbumRows = artistIds.Count == 0
            ? []
            : await context.MusicArtistCredits
                .AsNoTracking()
                .Where(c => artistIds.Contains(c.MusicArtistId) && c.IsGuest && c.Media is MusicTrack)
                .Select(c => new { c.MusicArtistId, AlbumId = ((MusicTrack)c.Media).AlbumId })
                .Distinct()
                .ToListAsync(cancellationToken);

        var linkedAlbumIds = artistAlbumRows
            .Select(a => a.Id)
            .Concat(guestAlbumRows.Select(a => a.AlbumId))
            .ToHashSet();
        if (linkedAlbumIds.Count > 0)
        {
            var linkedAlbumRows = await context.Medias
                .OfType<MusicAlbum>()
                .AsNoTracking()
                .Where(a => linkedAlbumIds.Contains(a.Id))
                .Select(a => new AlbumRow(
                    a.Id,
                    a.ArtistId,
                    a.Artist != null ? a.Artist.Title : null))
                .ToListAsync(cancellationToken);
            foreach (var album in linkedAlbumRows)
                albumById[album.Id] = album;

            var linkedBaseRows = await context.Medias
                .OfType<MusicAlbum>()
                .AsNoTracking()
                .Where(a => linkedAlbumIds.Contains(a.Id))
                .Select(a => new BaseRow(a.Id, a.Type, a.Title, a.SortTitle, a.ReleaseDate, a.Created))
                .ToListAsync(cancellationToken);
            foreach (var album in linkedBaseRows)
                baseById[album.Id] = album;
        }

        var serieIds = episodeRows.Select(e => e.SerieId).ToHashSet();
        var seasonCountsBySerieId = serieIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await context.Medias
                .OfType<SerieSeason>()
                .AsNoTracking()
                .Where(s => serieIds.Contains(s.SerieId))
                .GroupBy(s => s.SerieId)
                .Select(g => new { SerieId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SerieId, x => x.Count, cancellationToken);

        var pictureMediaIds = idSet
            .Concat(trackRows.Select(t => t.AlbumId))
            .Concat(episodeRows.Select(e => e.SerieId))
            .Concat(episodeRows.Select(e => e.SeasonId))
            .Concat(artistAlbumRows.Select(a => a.Id))
            .Concat(guestAlbumRows.Select(a => a.AlbumId))
            .ToHashSet();
        var pictures = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.MediaId.HasValue && pictureMediaIds.Contains(p.MediaId.Value))
            .Select(p => new PictureRow(p.Id, p.MediaId!.Value, p.Type, p.LocalPath != null, p.DominantColor, p.OriginalWidth, p.OriginalHeight))
            .ToListAsync(cancellationToken);
        var pictureSizes = await MetadataPictureSizesHelper.GetAvailableSizesByPictureIdsAsync(context, pictures.Select(p => p.Id), cancellationToken);
        var picturesByMediaId = pictures
            .GroupBy(p => p.MediaId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MetadataPictureDto>)g.Select(p => MapPicture(p, pictureSizes)).ToList());

        var files = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId.HasValue && idSet.Contains(f.MediaId.Value))
            .Select(f => new FileRow(
                f.MediaId!.Value,
                f.Id,
                (f.FileMetadata as AudioFileMetadata)!.Duration.TotalSeconds))
            .ToListAsync(cancellationToken);
        var remoteFiles = await context.RemoteIndexedFiles
            .AsNoTracking()
            .Where(f => idSet.Contains(f.MediaId))
            .Select(f => new FileRow(f.MediaId, f.Id, f.Duration.HasValue ? f.Duration.Value.TotalSeconds : null))
            .ToListAsync(cancellationToken);
        var indexedFileByMediaId = files.GroupBy(f => f.MediaId).ToDictionary(g => g.Key, g => g.First());
        var remoteFileByMediaId = remoteFiles.GroupBy(f => f.MediaId).ToDictionary(g => g.Key, g => g.First());

        var videoFiles = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId.HasValue && idSet.Contains(f.MediaId.Value))
            .Select(f => new FileRow(
                f.MediaId!.Value,
                f.Id,
                (f.FileMetadata as VideoFileMetadata)!.Duration.TotalSeconds))
            .ToListAsync(cancellationToken);
        var videoFileByMediaId = videoFiles.GroupBy(f => f.MediaId).ToDictionary(g => g.Key, g => g.First());

        var credits = trackRows.Count == 0
            ? []
            : await context.MusicArtistCredits
                .AsNoTracking()
                .Where(c => trackById.Keys.Contains(c.MediaId))
                .OrderBy(c => c.Order)
                .Select(c => new { c.MediaId, c.MusicArtistId, ArtistName = c.MusicArtist.Title, c.IsGuest })
                .ToListAsync(cancellationToken);
        var creditsByTrackId = credits
            .GroupBy(c => c.MediaId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MusicArtistCreditDto>)g.Select(c => new MusicArtistCreditDto
                {
                    ArtistId = c.MusicArtistId,
                    ArtistName = c.ArtistName ?? "",
                    IsGuest = c.IsGuest
                }).ToList());

        var states = userId.HasValue
            ? await context.UserMediaStates
                .AsNoTracking()
                .Where(s => s.UserId == userId.Value && idSet.Contains(s.MediaId))
                .ToListAsync(cancellationToken)
            : [];
        var statesByMediaId = states.ToDictionary(s => s.MediaId, s => s.ToUserMediaStateDto());
        var ratings = userId.HasValue
            ? await context.Ratings
                .OfType<UserRating>()
                .AsNoTracking()
                .Where(r => r.UserId == userId.Value && idSet.Contains(r.MediaId))
                .Select(r => new { r.MediaId, r.Value })
                .ToListAsync(cancellationToken)
            : [];
        var ratingsByMediaId = ratings.ToDictionary(r => r.MediaId, r => (int?)r.Value);

        var resultById = new Dictionary<Guid, LiteMediaDto>();
        foreach (var row in baseRows)
        {
            var common = new
            {
                row.Id,
                row.Title,
                row.SortTitle,
                row.ReleaseDate,
                row.Created,
                Pictures = GetPictures(picturesByMediaId, row.Id),
                UserState = statesByMediaId.GetValueOrDefault(row.Id),
                UserRating = ratingsByMediaId.GetValueOrDefault(row.Id)
            };

            resultById[row.Id] = row.Type switch
            {
                MediaType.Movie => new LiteMovieDto { Id = common.Id, Title = common.Title, SortTitle = common.SortTitle, ReleaseDate = common.ReleaseDate, Created = common.Created, Pictures = common.Pictures, UserState = common.UserState, UserRating = common.UserRating },
                MediaType.MusicAlbum when albumById.TryGetValue(row.Id, out var album) => new LiteMusicAlbumDto
                {
                    Id = common.Id,
                    Title = common.Title,
                    SortTitle = common.SortTitle,
                    ReleaseDate = common.ReleaseDate,
                    Created = common.Created,
                    Pictures = common.Pictures,
                    ArtistId = album.ArtistId,
                    ArtistName = album.ArtistName,
                    UserState = common.UserState,
                    UserRating = common.UserRating
                },
                MediaType.Serie => new LiteSerieDto { Id = common.Id, Title = common.Title, SortTitle = common.SortTitle, ReleaseDate = common.ReleaseDate, Created = common.Created, Pictures = common.Pictures, UserState = common.UserState, UserRating = common.UserRating },
                MediaType.MusicTrack when trackById.TryGetValue(row.Id, out var track) => new LiteMusicTrackDto
                {
                    Id = common.Id, Title = common.Title, SortTitle = common.SortTitle, ReleaseDate = common.ReleaseDate, Created = common.Created,
                    Pictures = GetPictures(picturesByMediaId, track.AlbumId) ?? common.Pictures, AlbumId = track.AlbumId, TrackNumber = track.TrackNumber,
                    IndexedFileId = indexedFileByMediaId.GetValueOrDefault(row.Id)?.Id, RemoteIndexedFileId = remoteFileByMediaId.GetValueOrDefault(row.Id)?.Id,
                    Duration = indexedFileByMediaId.GetValueOrDefault(row.Id)?.Duration ?? remoteFileByMediaId.GetValueOrDefault(row.Id)?.Duration,
                    AlbumTitle = track.AlbumTitle, ArtistName = track.ArtistName ?? track.AlbumArtistName, ArtistId = track.ArtistId ?? track.AlbumArtistId,
                    Genre = track.Genre, LoudnessLufs = track.LoudnessLufs, FadeInDuration = track.FadeInDuration, FadeOutDuration = track.FadeOutDuration,
                    ReplayGainTrackGain = track.ReplayGainTrackGain, ArtistCredits = creditsByTrackId.GetValueOrDefault(row.Id),
                    UserState = common.UserState, UserRating = common.UserRating
                },
                MediaType.SerieEpisode when episodeById.TryGetValue(row.Id, out var episode) => new LiteSerieEpisodeDto
                {
                    Id = common.Id, Title = common.Title, SortTitle = common.SortTitle, ReleaseDate = common.ReleaseDate, Created = common.Created, Pictures = common.Pictures,
                    EpisodeNumber = episode.EpisodeNumber, SeasonNumber = episode.SeasonNumber, SerieSeasonCount = seasonCountsBySerieId.GetValueOrDefault(episode.SerieId),
                    Duration = videoFileByMediaId.GetValueOrDefault(row.Id)?.Duration ?? remoteFileByMediaId.GetValueOrDefault(row.Id)?.Duration,
                    Overview = episode.Overview, SerieId = episode.SerieId, SerieTitle = episode.SerieTitle, SerieReleaseDate = episode.SerieReleaseDate,
                    StillImageId = pictures.FirstOrDefault(p => p.MediaId == row.Id && p.Type == MetadataPictureType.Still)?.Id,
                    IndexedFileId = videoFileByMediaId.GetValueOrDefault(row.Id)?.Id, RemoteIndexedFileId = remoteFileByMediaId.GetValueOrDefault(row.Id)?.Id,
                    SeriePictures = GetPictures(picturesByMediaId, episode.SerieId), SeasonPictures = GetPictures(picturesByMediaId, episode.SeasonId),
                    UserState = common.UserState, UserRating = common.UserRating
                },
                MediaType.SerieSeason when seasonById.TryGetValue(row.Id, out var season) => new LiteSerieSeasonDto
                {
                    Id = common.Id, Title = common.Title, SortTitle = common.SortTitle, ReleaseDate = common.ReleaseDate, Created = common.Created, Pictures = common.Pictures,
                    SerieId = season.SerieId, SerieTitle = season.SerieTitle, SeasonNumber = season.SeasonNumber, EpisodeCount = season.EpisodeCount,
                    Poster = common.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster),
                    SeriePictures = GetPictures(picturesByMediaId, season.SerieId), UserState = common.UserState, UserRating = common.UserRating
                },
                MediaType.MusicArtist when artistById.TryGetValue(row.Id, out var artist) => new LiteMusicArtistDto
                {
                    Id = common.Id, Title = common.Title, SortTitle = common.SortTitle, ReleaseDate = common.ReleaseDate, Created = common.Created, Pictures = common.Pictures,
                    ArtistType = artist.ArtistType, Country = artist.Country,
                    Albums = ToLiteAlbums(artistAlbumRows.Where(a => a.ArtistId == row.Id).Select(a => a.Id), baseById, picturesByMediaId, albumById),
                    GuestAppearanceAlbums = ToLiteAlbums(guestAlbumRows.Where(a => a.MusicArtistId == row.Id).Select(a => a.AlbumId), baseById, picturesByMediaId, albumById),
                    UserState = common.UserState, UserRating = common.UserRating
                },
                _ => throw new NotSupportedException($"Unknown media type: {row.Type}")
            };
        }

        return mediaIds.Where(resultById.ContainsKey).Select(id => resultById[id]).ToList();
    }

    public async Task<List<BaseMedia>> GetLiteMediasAsync(
        IReadOnlyList<Guid> mediaIds,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (mediaIds.Count == 0)
            return [];

        var idSet = mediaIds.ToHashSet();
        var items = await context.Medias
            .Where(m => idSet.Contains(m.Id))
            .ApplyLiteMappingIncludes(userId)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var itemsById = items.ToDictionary(m => m.Id);
        return mediaIds.Where(itemsById.ContainsKey).Select(id => itemsById[id]).ToList();
    }

    private static MetadataPictureDto MapPicture(
        PictureRow picture,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>> sizes) => new()
    {
        Id = picture.Id,
        Type = picture.Type,
        Uri = picture.IsLocal ? new Uri($"/api/metadata-pictures/{picture.Id}", UriKind.Relative) : null,
        DominantColor = picture.DominantColor,
        OriginalWidth = picture.OriginalWidth,
        OriginalHeight = picture.OriginalHeight,
        AvailableSizes = sizes.GetValueOrDefault(picture.Id) ?? []
    };

    private static IReadOnlyList<MetadataPictureDto>? GetPictures(
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureDto>> picturesByMediaId,
        Guid mediaId) => picturesByMediaId.GetValueOrDefault(mediaId) ?? [];

    private static IReadOnlyList<LiteMusicAlbumDto>? ToLiteAlbums(
        IEnumerable<Guid> albumIds,
        IReadOnlyDictionary<Guid, BaseRow> baseById,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureDto>> picturesByMediaId,
        IReadOnlyDictionary<Guid, AlbumRow> albumById)
    {
        var albums = albumIds
            .Distinct()
            .Where(baseById.ContainsKey)
            .Select(id => baseById[id])
            .Select(a =>
            {
                albumById.TryGetValue(a.Id, out var albumArtist);
                return new LiteMusicAlbumDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    SortTitle = a.SortTitle,
                    ReleaseDate = a.ReleaseDate,
                    Created = a.Created,
                    Pictures = GetPictures(picturesByMediaId, a.Id),
                    ArtistId = albumArtist?.ArtistId,
                    ArtistName = albumArtist?.ArtistName
                };
            })
            .ToList();
        return albums.Count > 0 ? albums : null;
    }
}
