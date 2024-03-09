using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class MusicAlbum() : BaseMedia(MediaType.MusicAlbum)
{
    public int? ArtistId { get; set; }

    public virtual MusicArtist? Artist { get; set; }
    public virtual IEnumerable<MusicTrack>? Tracks { get; set; }
    public virtual new MusicAlbumMetadata? Metadata { get; set; }
}
