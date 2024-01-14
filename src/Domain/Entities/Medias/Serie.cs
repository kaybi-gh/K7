namespace MediaServer.Domain.Entities.Medias;
public class Serie : BaseMedia
{
    public Serie() : base(MediaType.Serie) { }

    public required string Title { get; set; }
    public DateOnly? ReleaseYear { get; set; }
    public IEnumerable<SerieSeason> Seasons { get; set; } = [];
    public IEnumerable<SerieEpisode> Episodes { get; set; } = [];
}
