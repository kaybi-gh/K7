using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;

namespace K7.Server.Application.Common.Mappings;

public static class PlaylistMappings
{
    extension(Playlist domain)
    {
        public PlaylistDto ToPlaylistDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            Description = domain.Description,
            UserId = domain.UserId,
            IsSmartPlaylist = domain is SmartPlaylist,
            CoverPicture = domain.CoverPicture?.ToMetadataPictureDto(),
            ItemCount = domain.Items.Count,
            Created = domain.Created,
            LastModified = domain.LastModified
        };

        public LitePlaylistDto ToLitePlaylistDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            Description = domain.Description,
            IsSmartPlaylist = domain is SmartPlaylist,
            CoverPicture = domain.CoverPicture?.ToMetadataPictureDto(),
            ItemCount = domain.Items.Count,
            Created = domain.Created,
            LastModified = domain.LastModified
        };
    }

    extension(SmartPlaylist domain)
    {
        public SmartPlaylistDto ToSmartPlaylistDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            Description = domain.Description,
            UserId = domain.UserId,
            MediaType = domain.MediaType,
            MatchCondition = domain.MatchCondition,
            Rules = domain.Rules.Select(r => new SmartPlaylistRuleDto
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList(),
            Limit = domain.Limit,
            OrderBy = domain.OrderBy,
            OrderDescending = domain.OrderDescending,
            CoverPicture = domain.CoverPicture?.ToMetadataPictureDto(),
            ItemCount = domain.Items.Count,
            LastEvaluatedAt = domain.LastEvaluatedAt,
            Created = domain.Created,
            LastModified = domain.LastModified
        };

        public LiteSmartPlaylistDto ToLiteSmartPlaylistDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            Description = domain.Description,
            MediaType = domain.MediaType,
            RuleCount = domain.Rules.Count,
            CoverPicture = domain.CoverPicture?.ToMetadataPictureDto(),
            Created = domain.Created,
            LastModified = domain.LastModified
        };
    }

    extension(PlaylistItem domain)
    {
        public PlaylistItemDto ToPlaylistItemDto()
        {
            var media = domain.Media;
            var indexedFile = media?.IndexedFiles.FirstOrDefault();

            var artistRole = media is MusicTrack track
                ? track.Album?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()
                : media?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault();

            var genre = media is MusicTrack t2
                ? (t2.Album?.Genres?.FirstOrDefault() ?? t2.Genres?.FirstOrDefault())
                : media?.Genres?.FirstOrDefault();

            return new()
            {
                Id = domain.Id,
                MediaId = domain.MediaId,
                Order = domain.Order,
                MediaTitle = media?.Title,
                ArtistName = artistRole?.Person?.Name,
                ArtistPersonId = artistRole?.PersonId,
                AlbumTitle = media is MusicTrack t ? t.Album?.Title : null,
                Genre = genre,
                IndexedFileId = indexedFile?.Id,
                Duration = (indexedFile?.FileMetadata as AudioFileMetadata)?.Duration.TotalSeconds,
                UserRating = media?.Ratings.OfType<UserRating>().FirstOrDefault()?.Value is double v ? (int)v : null,
                Bpm = media is MusicTrack mt ? mt.AudioAnalysis?.Bpm : null,
                MusicalKey = media is MusicTrack mk ? mk.AudioAnalysis?.MusicalKey : null,
                Energy = media is MusicTrack me ? me.AudioAnalysis?.Energy : null,
                Pictures = media?.Pictures.Select(p => p.ToMetadataPictureDto()).ToList()
            };
        }
    }
}
