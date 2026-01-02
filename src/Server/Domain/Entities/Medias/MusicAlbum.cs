namespace K7.Server.Domain.Entities.Medias;
public class MusicAlbum() : BaseMedia(MediaType.MusicAlbum)
{
    public virtual IList<MusicTrack> Tracks { get; set; } = [];

    public override string GetSlugSource() => $"{Title}-{ReleaseDate?.Year}";
}
