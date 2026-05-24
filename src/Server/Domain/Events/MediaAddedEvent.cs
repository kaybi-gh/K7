using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Events;

public class MediaAddedEvent : BaseEvent
{
    public MediaAddedEvent(BaseMedia media)
    {
        Media = media;

        var pictureType = media.Type is MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist
            ? MetadataPictureType.Cover
            : MetadataPictureType.Poster;

        PictureUrl = (media.Pictures.FirstOrDefault(p => p.Type == pictureType)
            ?? media.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? media.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Cover))
            ?.OriginalRemoteUri?.AbsoluteUri;

        BackdropUrl = media.Pictures
            .FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)
            ?.OriginalRemoteUri?.AbsoluteUri;
    }

    public BaseMedia Media { get; }
    public string? PictureUrl { get; }
    public string? BackdropUrl { get; }
}
