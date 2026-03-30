using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Common.Mappings;

public static class MediaMappings
{
    extension(BaseMedia domain)
    {
        public MediaDto ToMediaDto() => domain switch
        {
            Movie movie => new MovieDto()
            {
                Id = domain.Id,
                Slug = domain.Slug,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                Overview = movie.Overview,
                OriginalLanguage = movie.OriginalLanguage,
                TagLine = movie.Tagline,
                ContentRating = movie.ContentRating,
                Budget = movie.Budget,
                Revenue = movie.Revenue,
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null
            },
            MusicAlbum album => new MusicAlbumDto()
            {
                Id = domain.Id,
                Slug = domain.Slug,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                Overview = album.Overview,
                Tracks = album.Tracks.Select(t => (LiteMusicTrackDto)t.ToLiteMediaDto()).ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null
            },
            MusicTrack track => new MusicTrackDto()
            {
                Id = domain.Id,
                Slug = domain.Slug,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                AlbumId = track.AlbumId,
                TrackNumber = track.TrackNumber,
                DiscNumber = track.DiscNumber,
                Lyrics = track.Lyrics,
                LyricsLrc = track.LyricsLrc,
                Bpm = track.AudioAnalysis?.Bpm,
                MusicalKey = track.AudioAnalysis?.MusicalKey,
                LoudnessLufs = track.AudioAnalysis?.LoudnessLufs,
                Energy = track.AudioAnalysis?.Energy,
                Danceability = track.AudioAnalysis?.Danceability,
                Valence = track.AudioAnalysis?.Valence,
                WaveformPeaks = track.AudioAnalysis?.WaveformPeaks,
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null
            },
            Serie serie => new SerieDto()
            {
                Id = domain.Id,
                Slug = domain.Slug,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                Overview = serie.Overview,
                Status = serie.Status,
                OriginalLanguage = serie.OriginalLanguage,
                ContentRating = serie.ContentRating,
                Network = serie.Network,
                Seasons = serie.Seasons
                    .OrderBy(s => s.SeasonNumber)
                    .Select(s => new LiteSerieSeasonDto
                    {
                        Id = s.Id,
                        SeasonNumber = s.SeasonNumber,
                        Title = s.Title,
                        EpisodeCount = s.Episodes.Count,
                        Poster = s.Pictures
                            .Where(p => p.Type == MetadataPictureType.Poster)
                            .Select(p => p.ToMetadataPictureDto())
                            .FirstOrDefault()
                    })
                    .ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null
            },
            SerieSeason season => new SerieSeasonDto()
            {
                Id = domain.Id,
                Slug = domain.Slug,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                SeasonNumber = season.SeasonNumber,
                Overview = season.Overview,
                SerieId = season.SerieId,
                SerieTitle = season.Serie?.Title,
                Episodes = season.Episodes
                    .OrderBy(e => e.EpisodeNumber)
                    .Select(e => (LiteSerieEpisodeDto)e.ToLiteMediaDto())
                    .ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null
            },
            SerieEpisode episode => new SerieEpisodeDto()
            {
                Id = domain.Id,
                Slug = domain.Slug,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                EpisodeNumber = episode.EpisodeNumber,
                SeasonNumber = episode.Season?.SeasonNumber ?? 0,
                Overview = episode.Overview,
                AirDate = episode.AirDate,
                Runtime = episode.Runtime,
                SerieId = episode.SerieId,
                SeasonId = episode.SeasonId,
                SerieTitle = episode.Serie?.Title,
                SeasonTitle = episode.Season?.Title,
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null
            },
            _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
        };

        public LiteMediaDto ToLiteMediaDto() => domain switch
        {
            Movie movie => new LiteMovieDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate?.ToString(),
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                UserRating = GetUserRating(domain)
            },
            MusicAlbum album => new LiteMusicAlbumDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate?.ToString(),
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                UserRating = GetUserRating(domain)
            },
            MusicTrack track => new LiteMusicTrackDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate?.ToString(),
                Pictures = (track.Album?.Pictures ?? domain.Pictures).Select(p => p.ToMetadataPictureDto()).ToList(),
                AlbumId = track.AlbumId,
                TrackNumber = track.TrackNumber,
                IndexedFileId = domain.IndexedFiles.FirstOrDefault()?.Id,
                Duration = (domain.IndexedFiles.FirstOrDefault()?.FileMetadata as AudioFileMetadata)?.Duration.TotalSeconds,
                AlbumTitle = track.Album?.Title,
                ArtistName = track.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()?.Person?.Name
                           ?? track.Album?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()?.Person?.Name,
                ArtistPersonId = track.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()?.PersonId
                               ?? track.Album?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()?.PersonId,
                Genre = track.Album?.Genres.FirstOrDefault() ?? domain.Genres.FirstOrDefault(),
                Bpm = track.AudioAnalysis?.Bpm,
                MusicalKey = track.AudioAnalysis?.MusicalKey,
                Energy = track.AudioAnalysis?.Energy,
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                UserRating = GetUserRating(domain)
            },
            Serie => new LiteSerieDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate?.ToString(),
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                UserRating = GetUserRating(domain)
            },
            SerieEpisode episode => new LiteSerieEpisodeDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate?.ToString(),
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                EpisodeNumber = episode.EpisodeNumber,
                SeasonNumber = episode.Season?.SeasonNumber ?? 0,
                Duration = (domain.IndexedFiles.FirstOrDefault()?.FileMetadata as VideoFileMetadata)?.Duration.TotalSeconds,
                SerieId = episode.SerieId,
                SerieTitle = episode.Serie?.Title,
                StillImageId = domain.Pictures
                    .Where(p => p.Type == MetadataPictureType.Still)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefault(),
                SeriePictures = episode.Serie?.Pictures?.Select(p => p.ToMetadataPictureDto()).ToList(),
                SeasonPictures = episode.Season?.Pictures?.Select(p => p.ToMetadataPictureDto()).ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                UserRating = GetUserRating(domain)
            },
            _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
        };
    }

    extension(BaseRating domain)
    {
        public RatingDto ToRatingDto() => new()
        {
            Id = domain.Id,
            Source = domain.Source,
            Value = domain.Value,
            MinimumValue = domain.MinimumValue,
            MaximumValue = domain.MaximumValue
        };
    }

    extension(MediaDto dto)
    {
        public BaseMedia ToDomainEntity() => dto switch
        {
            MovieDto => new Movie(),
            MusicAlbumDto => new MusicAlbum(),
            MusicTrackDto => new MusicTrack(),
            SerieDto => new Serie(),
            SerieSeasonDto => new SerieSeason(),
            SerieEpisodeDto => new SerieEpisode(),
            _ => throw new NotSupportedException($"Unknown type: {dto.GetType().Name}")
        };
    }

    private static int? GetUserRating(BaseMedia domain) =>
        domain.Ratings.OfType<UserRating>().FirstOrDefault()?.Value is double v ? (int)v : null;
}
