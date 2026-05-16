namespace K7.Server.Domain.Entities.Medias;
public class MusicTrack() : BaseMedia(MediaType.MusicTrack)
{
    public Guid AlbumId { get; set; }
    public MusicAlbum Album { get; set; } = null!;

    public Guid? ArtistId { get; set; }
    public MusicArtist? Artist { get; set; }

    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }

    public string? Lyrics { get; set; }
    public string? LyricsLrc { get; set; }

    public AudioAnalysis? AudioAnalysis { get; set; }

    public IList<MusicArtistCredit> ArtistCredits { get; set; } = [];
}
