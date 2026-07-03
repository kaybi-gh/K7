using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Common.Mappings;

public static class FederationSocialMappings
{
    extension(BaseMedia media)
    {
        public FederatedMediaRef ToFederatedMediaRef()
        {
            var title = media switch
            {
                Movie m => m.Title,
                MusicAlbum a => a.Title,
                MusicTrack t => t.Title,
                Serie s => s.Title,
                SerieEpisode e => e.Title,
                SerieSeason ss => ss.Title,
                _ => null
            };

            return new FederatedMediaRef
            {
                RemoteMediaId = media.Id,
                Type = media.Type,
                Title = title,
                ExternalIds = media.ExternalIds
                    .Where(e => e.ProviderName != "federation")
                    .Select(e => new PeerExternalIdDto
                    {
                        Provider = e.ProviderName,
                        Value = e.Value
                    })
                    .ToList()
            };
        }

        public Guid? GetCoverPictureId() => ResolveCoverPictureId(media);

        public SocialUserMediaCardDto ToSocialUserMediaCard(
            FederatedSocialItemStatus status,
            Guid? localMediaId = null,
            Guid? remoteIndexedFileId = null) =>
            new()
            {
                Media = media.ToFederatedMediaRef(),
                Status = status,
                LocalMediaId = localMediaId ?? media.Id,
                RemoteIndexedFileId = remoteIndexedFileId,
                CoverPictureId = media.GetCoverPictureId()
            };
    }

    private static Guid? ResolveCoverPictureId(BaseMedia media) =>
        media switch
        {
            MusicTrack track =>
                GetPictureIdFromPictures(track.Pictures, MetadataPictureType.Cover, MetadataPictureType.Poster)
                ?? (track.Album is not null
                    ? GetPictureIdFromPictures(track.Album.Pictures, MetadataPictureType.Cover, MetadataPictureType.Poster)
                    : null),
            SerieEpisode episode =>
                GetPictureIdFromPictures(episode.Pictures, MetadataPictureType.Still, MetadataPictureType.Poster)
                ?? (episode.Season is not null
                    ? GetPictureIdFromPictures(episode.Season.Pictures, MetadataPictureType.Poster, MetadataPictureType.Backdrop)
                    : null)
                ?? (episode.Serie is not null
                    ? GetPictureIdFromPictures(episode.Serie.Pictures, MetadataPictureType.Poster, MetadataPictureType.Backdrop)
                    : null),
            SerieSeason season =>
                GetPictureIdFromPictures(season.Pictures, MetadataPictureType.Poster, MetadataPictureType.Backdrop)
                ?? (season.Serie is not null
                    ? GetPictureIdFromPictures(season.Serie.Pictures, MetadataPictureType.Poster, MetadataPictureType.Backdrop)
                    : null),
            _ => GetPictureIdFromPictures(
                media.Pictures,
                media.Type switch
                {
                    MediaType.MusicAlbum or MediaType.MusicArtist =>
                        [MetadataPictureType.Cover, MetadataPictureType.Poster],
                    _ => [MetadataPictureType.Poster, MetadataPictureType.Backdrop]
                })
        };

    private static Guid? GetPictureIdFromPictures(
        IEnumerable<MetadataPicture> pictures,
        params MetadataPictureType[] preferredTypes)
    {
        foreach (var type in preferredTypes)
        {
            var picture = pictures.FirstOrDefault(p => p.Type == type);
            if (picture is not null)
                return picture.Id;
        }

        return pictures.FirstOrDefault()?.Id;
    }
}
