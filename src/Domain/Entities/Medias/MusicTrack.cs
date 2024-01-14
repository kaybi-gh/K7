namespace MediaServer.Domain.Entities.Medias;
public class MusicTrack : BaseMedia
{
    public MusicTrack() : base(MediaType.MusicTrack) { }

    public int Number { get; set; }
    public int? ArtistId { get; set; }
    public int? AlbumId { get; set; }
    public MusicArtist? Artist { get; set; }
    public MusicAlbum? Album { get; set; }
}
