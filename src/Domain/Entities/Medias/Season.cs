namespace MediaServer.Domain.Entities.Medias;
public class Season : BaseMedia
{
    public int Number { get; set; }
    public int SerieId { get; set; }
    public IEnumerable<Episode> Episodes { get; set; } = [];
}
