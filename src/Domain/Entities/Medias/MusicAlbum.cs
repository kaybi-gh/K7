namespace MediaServer.Domain.Entities.Medias;
public class MusicAlbum : BaseMedia
{
    public IEnumerable<Track> Tracks { get; set; } = [];
}
