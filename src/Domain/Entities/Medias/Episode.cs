namespace MediaServer.Domain.Entities.Medias;
public class Episode : BaseMedia
{
    public int Number { get; set; }
    public int? SeasonId { get; set; }
    public Season? Season { get; set; }
}
