using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Entities.Medias;
public class MusicAlbum(MediaIdentification identification) : BaseMedia(MediaType.MusicAlbum, identification)
{
    public int? ArtistId { get; set; }

    public virtual MusicArtist? Artist { get; set; }
    public virtual IEnumerable<MusicTrack> Tracks { get; set; } = [];
    public virtual new MusicAlbumMetadata? Metadata { get; set; }
}
