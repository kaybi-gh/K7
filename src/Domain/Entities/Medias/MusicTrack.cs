using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class MusicTrack() : BaseMedia(MediaType.MusicTrack)
{
    public int Number { get; set; }
    public int? ArtistId { get; set; }
    public int? AlbumId { get; set; }
    
    public virtual MusicArtist? Artist { get; set; }
    public virtual MusicAlbum? Album { get; set; }
    public virtual new MusicTrackMetadata? Metadata { get; set; }
}
