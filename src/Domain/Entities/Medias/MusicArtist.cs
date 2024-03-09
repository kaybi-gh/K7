using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class MusicArtist : BaseMedia
{
    public MusicArtist() : base(MediaType.MusicArtist) { }

    public virtual IEnumerable<MusicAlbum> Album { get; set; } = [];
    public virtual IEnumerable<MusicTrack> Tracks { get; set; } = [];
    public virtual MusicArtistMetadata? Metadata { get; set; }
}
