using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
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
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                RemoteIndexedFiles = domain.RemoteIndexedFiles.Select(f => f.ToRemoteIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                LockedFields = domain.LockedFields.ToList(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
                Overview = movie.Overview,
                OriginalLanguage = movie.OriginalLanguage,
                TagLine = movie.Tagline,
                ContentRating = movie.ContentRating,
                Budget = movie.Budget,
                Revenue = movie.Revenue,
                Studios = movie.Studios?.ToList() ?? [],
                Trailers = domain.Trailers?.Select(t => t.ToTrailerDto()).ToList() ?? [],
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                LastMetadataRefreshedAt = domain.LastMetadataRefreshedAt
            },
            MusicAlbum album => new MusicAlbumDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                RemoteIndexedFiles = domain.RemoteIndexedFiles.Select(f => f.ToRemoteIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                LockedFields = domain.LockedFields.ToList(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
                Overview = album.Overview,
                ArtistId = album.ArtistId,
                ArtistName = album.Artist?.Title,
                Tracks = album.Tracks.Select(t => (LiteMusicTrackDto)t.ToLiteMediaDto()).ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                LastMetadataRefreshedAt = domain.LastMetadataRefreshedAt
            },
            MusicTrack track => new MusicTrackDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                RemoteIndexedFiles = domain.RemoteIndexedFiles.Select(f => f.ToRemoteIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                LockedFields = domain.LockedFields.ToList(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
                AlbumId = track.AlbumId,
                ArtistId = track.ArtistId ?? track.Album?.ArtistId,
                ArtistName = track.Artist?.Title ?? track.Album?.Artist?.Title,
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
                FadeInDuration = track.AudioAnalysis?.FadeInDuration,
                FadeOutDuration = track.AudioAnalysis?.FadeOutDuration,
                ReplayGainTrackGain = track.AudioAnalysis?.ReplayGainTrackGain,
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                LastMetadataRefreshedAt = domain.LastMetadataRefreshedAt
            },
            MusicArtist artist => new MusicArtistDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                RemoteIndexedFiles = domain.RemoteIndexedFiles.Select(f => f.ToRemoteIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                LockedFields = domain.LockedFields.ToList(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
                ArtistType = artist.ArtistType,
                Biography = artist.Biography,
                Country = artist.Country,
                Albums = artist.Albums.Select(a => (MusicAlbumDto)a.ToMediaDto()).ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                LastMetadataRefreshedAt = domain.LastMetadataRefreshedAt
            },
            Serie serie => new SerieDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                RemoteIndexedFiles = domain.RemoteIndexedFiles.Select(f => f.ToRemoteIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                LockedFields = domain.LockedFields.ToList(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
                Overview = serie.Overview,
                Status = serie.Status,
                OriginalLanguage = serie.OriginalLanguage,
                ContentRating = serie.ContentRating,
                Network = serie.Network,
                Studios = serie.Studios?.ToList() ?? [],
                Trailers = domain.Trailers?.Select(t => t.ToTrailerDto()).ToList() ?? [],
                Seasons = serie.Seasons
                    .OrderBy(s => s.SeasonNumber)
                    .Select(s => new LiteSerieSeasonDto
                    {
                        Id = s.Id,
                        Title = s.Title,
                        ReleaseDate = s.ReleaseDate,
                        Pictures = s.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                        SerieId = s.SerieId,
                        SeasonNumber = s.SeasonNumber,
                        EpisodeCount = s.Episodes.Count,
                        Poster = s.Pictures
                            .Where(p => p.Type == MetadataPictureType.Poster)
                            .Select(p => p.ToMetadataPictureDto())
                            .FirstOrDefault(),
                        UserState = SeasonWatchStateHelper.AggregateFromEpisodes(s.Episodes.ToList())
                    })
                    .ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                LastMetadataRefreshedAt = domain.LastMetadataRefreshedAt
            },
            SerieSeason season => new SerieSeasonDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                RemoteIndexedFiles = domain.RemoteIndexedFiles.Select(f => f.ToRemoteIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                LockedFields = domain.LockedFields.ToList(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
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
                    : null,
                LastMetadataRefreshedAt = domain.LastMetadataRefreshedAt
            },
            SerieEpisode episode => new SerieEpisodeDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                PersonRoles = domain.PersonRoles.Select(r => r.ToLitePersonRoleDto()).ToList(),
                Ratings = domain.Ratings.Select(r => r.ToRatingDto()).ToList(),
                IndexedFiles = domain.IndexedFiles.Select(f => f.ToIndexedFileDto()).ToList(),
                RemoteIndexedFiles = domain.RemoteIndexedFiles.Select(f => f.ToRemoteIndexedFileDto()).ToList(),
                Genres = domain.Genres.ToList(),
                LockedFields = domain.LockedFields.ToList(),
                ExternalIds = domain.ExternalIds.Select(e => e.ToExternalIdDto()).ToList(),
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
                    : null,
                LastMetadataRefreshedAt = domain.LastMetadataRefreshedAt
            },
            _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
        };

        public LiteMediaDto ToLiteMediaDto() => domain switch
        {
            Movie movie => new LiteMovieDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Created = domain.Created,
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
                ReleaseDate = domain.ReleaseDate,
                Created = domain.Created,
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
                ReleaseDate = domain.ReleaseDate,
                Created = domain.Created,
                Pictures = (track.Album?.Pictures ?? domain.Pictures).Select(p => p.ToMetadataPictureDto()).ToList(),
                AlbumId = track.AlbumId,
                TrackNumber = track.TrackNumber,
                IndexedFileId = domain.IndexedFiles.FirstOrDefault()?.Id,
                RemoteIndexedFileId = domain.RemoteIndexedFiles.FirstOrDefault()?.Id,
                Duration = (domain.IndexedFiles.FirstOrDefault()?.FileMetadata as AudioFileMetadata)?.Duration.TotalSeconds
                    ?? domain.RemoteIndexedFiles.FirstOrDefault()?.Duration?.TotalSeconds,
                AlbumTitle = track.Album?.Title,
                ArtistName = track.Artist?.Title ?? track.Album?.Artist?.Title,
                ArtistId = track.ArtistId ?? track.Album?.ArtistId,
                Genre = track.Album?.Genres.FirstOrDefault() ?? domain.Genres.FirstOrDefault(),
                Bpm = track.AudioAnalysis?.Bpm,
                MusicalKey = track.AudioAnalysis?.MusicalKey,
                Energy = track.AudioAnalysis?.Energy,
                LoudnessLufs = track.AudioAnalysis?.LoudnessLufs,
                FadeInDuration = track.AudioAnalysis?.FadeInDuration,
                FadeOutDuration = track.AudioAnalysis?.FadeOutDuration,
                ReplayGainTrackGain = track.AudioAnalysis?.ReplayGainTrackGain,
                ArtistCredits = track.ArtistCredits.Count > 0
                    ? track.ArtistCredits.OrderBy(c => c.Order).Select(c => new MusicArtistCreditDto
                    {
                        ArtistId = c.MusicArtistId,
                        ArtistName = c.MusicArtist?.Title ?? "",
                        IsGuest = c.IsGuest
                    }).ToList()
                    : null,
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                UserRating = GetUserRating(domain)
            },
            MusicArtist artist => new LiteMusicArtistDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Created = domain.Created,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                ArtistType = artist.ArtistType,
                Country = artist.Country,
                Albums = artist.Albums.Count > 0
                    ? artist.Albums.Select(a => (LiteMusicAlbumDto)a.ToLiteMediaDto()).ToList()
                    : null,
                GuestAppearanceAlbums = artist.ArtistCredits.Count > 0
                    ? artist.ArtistCredits
                        .Where(c => c.IsGuest && c.Media is MusicTrack { Album: not null })
                        .Select(c => ((MusicTrack)c.Media).Album)
                        .DistinctBy(a => a.Id)
                        .Select(a => (LiteMusicAlbumDto)a.ToLiteMediaDto())
                        .ToList() is { Count: > 0 } guestAlbums ? guestAlbums : null
                    : null,
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                UserRating = GetUserRating(domain)
            },
            Serie => new LiteSerieDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Created = domain.Created,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                    ? state.ToUserMediaStateDto()
                    : null,
                UserRating = GetUserRating(domain)
            },
            SerieSeason season => new LiteSerieSeasonDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Created = domain.Created,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                SerieId = season.SerieId,
                SeasonNumber = season.SeasonNumber,
                EpisodeCount = season.Episodes.Count,
                Poster = domain.Pictures
                    .Where(p => p.Type == MetadataPictureType.Poster)
                    .Select(p => p.ToMetadataPictureDto())
                    .FirstOrDefault(),
                UserState = SeasonWatchStateHelper.AggregateFromEpisodes(season.Episodes.ToList())
                    ?? (domain.UserMediaStates.FirstOrDefault() is { } state
                        ? state.ToUserMediaStateDto()
                        : null),
                UserRating = GetUserRating(domain)
            },
            SerieEpisode episode => new LiteSerieEpisodeDto()
            {
                Id = domain.Id,
                Title = domain.Title,
                ReleaseDate = domain.ReleaseDate,
                Created = domain.Created,
                Pictures = domain.Pictures.Select(p => p.ToMetadataPictureDto()).ToList(),
                EpisodeNumber = episode.EpisodeNumber,
                SeasonNumber = episode.Season?.SeasonNumber ?? 0,
                SerieSeasonCount = episode.Serie?.Seasons?.Count ?? 1,
                Duration = (domain.IndexedFiles.FirstOrDefault()?.FileMetadata as VideoFileMetadata)?.Duration.TotalSeconds
                    ?? domain.RemoteIndexedFiles.FirstOrDefault()?.Duration?.TotalSeconds,
                Overview = episode.Overview,
                SerieId = episode.SerieId,
                SerieTitle = episode.Serie?.Title,
                SerieReleaseDate = episode.Serie?.ReleaseDate,
                StillImageId = domain.Pictures
                    .Where(p => p.Type == MetadataPictureType.Still)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefault(),
                IndexedFileId = domain.IndexedFiles.FirstOrDefault()?.Id,
                RemoteIndexedFileId = domain.RemoteIndexedFiles.FirstOrDefault()?.Id,
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
            MusicArtistDto => new MusicArtist(),
            SerieDto => new Serie(),
            SerieSeasonDto => new SerieSeason(),
            SerieEpisodeDto => new SerieEpisode(),
            _ => throw new NotSupportedException($"Unknown type: {dto.GetType().Name}")
        };
    }

    extension(TrailerInfo domain)
    {
        public TrailerDto ToTrailerDto() => new()
        {
            Key = domain.Key,
            Name = domain.Name,
            Site = domain.Site,
            Type = domain.Type,
            Language = domain.Language
        };
    }

    private static int? GetUserRating(BaseMedia domain) =>
        domain.Ratings.OfType<UserRating>().FirstOrDefault()?.Value is double v ? (int)v : null;
}
