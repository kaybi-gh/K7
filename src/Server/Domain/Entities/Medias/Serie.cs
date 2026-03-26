namespace K7.Server.Domain.Entities.Medias;
public class Serie() : BaseMedia(MediaType.Serie)
{
    public IList<SerieSeason> Seasons { get; set; } = [];
    public IList<SerieEpisode> Episodes { get; set; } = [];

    public override string GetSlugSource() => $"{Title}-{ReleaseDate?.Year}";
}
