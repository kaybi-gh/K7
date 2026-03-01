using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities.Users;

public class MediaPlaybackSession : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public Guid SessionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? LastUpdateAt { get; set; }
}
