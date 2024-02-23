using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class MusicTrack : BaseMedia
{
    public MusicTrack() : base(MediaType.MusicTrack) { }

    public int Number { get; set; }
    public int? ArtistId { get; set; }
    public int? AlbumId { get; set; }
    
    public virtual MusicArtist? Artist { get; set; }
    public virtual MusicAlbum? Album { get; set; }
    public virtual MusicTrackMetadata? Metadata { get; set; }
}
