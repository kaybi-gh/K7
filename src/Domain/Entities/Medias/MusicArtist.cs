using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Entities.Medias;
public class MusicArtist(MediaIdentification identification) : BaseMedia(MediaType.MusicArtist, identification)
{
    public virtual IEnumerable<MusicAlbum> Album { get; set; } = [];
    public virtual IEnumerable<MusicTrack> Tracks { get; set; } = [];
    public virtual new MusicArtistMetadata? Metadata { get; set; }
}
