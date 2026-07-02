namespace K7.Server.Domain.Entities.Users;

public class ViewingGroupMember : BaseEntity
{
    public Guid ViewingGroupId { get; set; }
    public ViewingGroup ViewingGroup { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
