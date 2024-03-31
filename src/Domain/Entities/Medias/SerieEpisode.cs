namespace MediaServer.Domain.Entities.Medias;
public class SerieEpisode() : BaseMedia(MediaType.Serie)
{
    public int? SerieId { get; set; }
    public virtual Serie? Serie { get; set; }
    public int? SeasonId { get; set; }
    public virtual SerieSeason? Season { get; set; }
}
