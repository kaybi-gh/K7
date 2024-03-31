namespace MediaServer.Domain.Entities.Medias;
public class MusicTrack() : BaseMedia(MediaType.MusicTrack)
{
    public int? ArtistId { get; set; }
    public virtual MusicArtist? Artist { get; set; }
    public int? AlbumId { get; set; }
    public virtual MusicAlbum? Album { get; set; }
}
