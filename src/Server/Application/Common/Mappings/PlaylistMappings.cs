using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
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
            MediaType = domain.MediaType,
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
            MediaType = domain.MediaType,
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
            RuleFilter = domain.RuleFilter.ToRuleGroupDto(),
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
            RuleCount = domain.RuleFilter.Items.Count,
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

            var artist = media is MusicTrack track
                ? (track.Artist ?? track.Album?.Artist)
                : (media as MusicAlbum)?.Artist;

            var genre = media is MusicTrack t2
                ? (GetFirstGenreTag(t2.Album) ?? GetFirstGenreTag(t2))
                : GetFirstGenreTag(media);

            var pictures = media is MusicTrack mt2
                ? (mt2.Pictures.Count != 0 ? mt2.Pictures : (mt2.Album?.Pictures ?? []))
                : (media?.Pictures ?? []);

            return new()
            {
                Id = domain.Id,
                MediaId = domain.MediaId,
                Order = domain.Order,
                MediaTitle = media?.Title,
                ArtistName = artist?.Title,
                ArtistId = artist?.Id,
                AlbumTitle = media is MusicTrack t ? t.Album?.Title : null,
                Genre = genre,
                IndexedFileId = indexedFile?.Id,
                Duration = (indexedFile?.FileMetadata as AudioFileMetadata)?.Duration.TotalSeconds,
                UserRating = media?.Ratings.OfType<UserRating>().FirstOrDefault()?.Value is double v ? (int)v : null,
                Pictures = pictures.Select(p => p.ToMetadataPictureDto()).ToList()
            };
        }
    }

    private static string? GetFirstGenreTag(BaseMedia? media) =>
        media?.MetadataTags
            .FirstOrDefault(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            ?.MetadataTag.DisplayName;
}
