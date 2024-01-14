namespace MediaServer.Domain.Entities.Medias;
public class SerieEpisode : BaseMedia
{
    public SerieEpisode() : base(MediaType.Serie) { }

    public int Number { get; set; }
    public string? Title { get; set; }
    public int? SerieId { get; set; }
    public int? SeasonId { get; set; }
    public Serie? Serie { get; set; }
    public SerieSeason? Season { get; set; }
}
