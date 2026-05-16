namespace K7.Server.Domain.Entities.Medias;

public class MusicArtist() : BaseMedia(MediaType.MusicArtist)
{
    public MusicArtistType ArtistType { get; set; } = MusicArtistType.Unknown;
    public string? Biography { get; set; }
    public string? Country { get; set; }

    public IList<MusicAlbum> Albums { get; set; } = [];
    public IList<MusicArtistCredit> ArtistCredits { get; set; } = [];
}
