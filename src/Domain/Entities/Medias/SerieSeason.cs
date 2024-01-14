namespace MediaServer.Domain.Entities.Medias;
public class SerieSeason : BaseMedia
{
    public SerieSeason() : base(MediaType.SerieSeason) { }

    public int Number { get; set; }
    public int SerieId { get; set; }
    public Serie? Serie { get; set; }
    public IEnumerable<SerieEpisode> Episodes { get; set; } = [];
}
