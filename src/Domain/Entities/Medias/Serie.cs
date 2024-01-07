namespace MediaServer.Domain.Entities.Medias;
public class Serie : BaseMedia
{
    public DateOnly ReleaseYear { get; set; }
    public IEnumerable<Season> Seasons { get; set; } = [];
}
