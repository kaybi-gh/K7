namespace MediaServer.Domain.Entities.Medias;
public class Track : BaseMedia
{
    public int Number { get; set; }
    public int AlbumId { get; set; }
    public MusicAlbum? Album { get; set; }
}
