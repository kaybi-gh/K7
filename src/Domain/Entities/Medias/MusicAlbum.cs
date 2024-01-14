namespace MediaServer.Domain.Entities.Medias;
public class MusicAlbum : BaseMedia
{
    public MusicAlbum() : base(MediaType.MusicAlbum) { }

    public int? ArtistId { get; set; }
    public MusicArtist? Artist { get; set; }
    public IEnumerable<MusicTrack> Tracks { get; set; } = [];
}
