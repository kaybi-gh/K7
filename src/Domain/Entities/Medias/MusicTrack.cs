using MediaServer.Domain.Entities.Metadatas.Persons;

namespace MediaServer.Domain.Entities.Medias;
public class MusicTrack() : BaseMedia(MediaType.MusicTrack)
{
    public int? AlbumId { get; set; }
    public virtual MusicAlbum? Album { get; set; }
}
