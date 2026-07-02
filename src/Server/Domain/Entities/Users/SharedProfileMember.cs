namespace K7.Server.Domain.Entities.Users;

public class SharedProfileMember : BaseEntity
{
    public Guid SharedProfileId { get; set; }
    public SharedProfile SharedProfile { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
