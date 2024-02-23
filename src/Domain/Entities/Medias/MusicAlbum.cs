using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class MusicAlbum : BaseMedia
{
    public MusicAlbum() : base(MediaType.MusicAlbum) { }

    public int? ArtistId { get; set; }

    public virtual MusicArtist? Artist { get; set; }
    public virtual IEnumerable<MusicTrack> Tracks { get; set; } = [];
    public virtual MusicAlbumMetadata? Metadata { get; set; }
}
