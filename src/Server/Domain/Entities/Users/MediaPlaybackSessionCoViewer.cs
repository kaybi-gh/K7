namespace K7.Server.Domain.Entities.Users;

public class MediaPlaybackSessionCoViewer : BaseEntity
{
    public Guid ReferenceId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
