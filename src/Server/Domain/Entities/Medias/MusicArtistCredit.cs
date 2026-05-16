namespace K7.Server.Domain.Entities.Medias;

public class MusicArtistCredit : BaseAuditableEntity
{
    public Guid MusicArtistId { get; set; }
    public MusicArtist MusicArtist { get; set; } = null!;

    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public bool IsGuest { get; set; }
    public int? Order { get; set; }
}
