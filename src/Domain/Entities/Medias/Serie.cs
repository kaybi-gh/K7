namespace MediaServer.Domain.Entities.Medias;
public class Serie() : BaseMedia(MediaType.Serie)
{
    public virtual IEnumerable<SerieSeason>? Seasons { get; set; }
    public virtual IEnumerable<SerieEpisode>? Episodes { get; set; }
}
