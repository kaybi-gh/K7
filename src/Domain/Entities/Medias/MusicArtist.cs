namespace MediaServer.Domain.Entities.Medias;
public class MusicArtist : BaseMedia
{
    public string? Name { get; set; }
    public IEnumerable<MusicAlbum> Album { get; set; } = [];
    public IEnumerable<MusicTrack> Tracks { get; set; } = [];
}
