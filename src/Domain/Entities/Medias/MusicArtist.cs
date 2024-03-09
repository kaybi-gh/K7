using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class MusicArtist() : BaseMedia(MediaType.MusicArtist)
{
    public virtual IEnumerable<MusicAlbum>? Album { get; set; }
    public virtual IEnumerable<MusicTrack>? Tracks { get; set; }
    public virtual new MusicArtistMetadata? Metadata { get; set; }
}
