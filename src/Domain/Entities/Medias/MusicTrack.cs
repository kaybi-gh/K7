using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Entities.Medias;
public class MusicTrack(MediaIdentification identification) : BaseMedia(MediaType.MusicTrack, identification)
{
    public int Number { get; set; }
    public int? ArtistId { get; set; }
    public int? AlbumId { get; set; }
    
    public virtual MusicArtist? Artist { get; set; }
    public virtual MusicAlbum? Album { get; set; }
    public virtual new MusicTrackMetadata? Metadata { get; set; }
}
