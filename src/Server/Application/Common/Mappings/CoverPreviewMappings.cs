using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class CoverPreviewMappings
{
    private const int MaxPreviewCount = 4;

    extension(Playlist playlist)
    {
        public IReadOnlyList<MetadataPictureDto> ToPreviewPictureDtos() =>
            GetPreviewPictures(playlist.Items.OrderBy(i => i.Order).Select(i => i.Media));
    }

    extension(Collection collection)
    {
        public IReadOnlyList<MetadataPictureDto> ToPreviewPictureDtos() =>
            GetPreviewPictures(collection.Items.OrderBy(i => i.Order).Select(i => i.Media));
    }

    private static IReadOnlyList<MetadataPictureDto> GetPreviewPictures(IEnumerable<BaseMedia> medias)
    {
        var pictures = new List<MetadataPictureDto>();

        foreach (var media in medias)
        {
            if (pictures.Count >= MaxPreviewCount)
                break;

            var picture = GetPrimaryPicture(media);
            if (picture is not null)
                pictures.Add(picture.ToMetadataPictureDto());
        }

        return pictures;
    }

    private static MetadataPicture? GetPrimaryPicture(BaseMedia media)
    {
        if (media is MusicTrack track)
        {
            return track.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? track.Album?.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? track.Pictures.FirstOrDefault()
                ?? track.Album?.Pictures.FirstOrDefault();
        }

        var pictureType = media.Type switch
        {
            MediaType.MusicAlbum or MediaType.MusicArtist => MetadataPictureType.Cover,
            MediaType.SerieEpisode => MetadataPictureType.Still,
            _ => MetadataPictureType.Poster
        };

        return media.Pictures.FirstOrDefault(p => p.Type == pictureType)
            ?? media.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? media.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? media.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
            ?? media.Pictures.FirstOrDefault();
    }
}
