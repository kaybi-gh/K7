namespace K7.Server.Domain.Entities.Users;

public class ViewingGroup : BaseAuditableEntity
{
    public required string Name { get; set; }
    public Guid HostUserId { get; set; }
    public User HostUser { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public string? PinHash { get; set; }

    public IList<ViewingGroupMember> Members { get; set; } = [];
}
