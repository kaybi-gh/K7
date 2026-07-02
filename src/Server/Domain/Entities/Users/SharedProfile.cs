namespace K7.Server.Domain.Entities.Users;

public class SharedProfile : BaseAuditableEntity
{
    public required string Name { get; set; }
    public Guid HostUserId { get; set; }
    public User HostUser { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public string? PinHash { get; set; }

    public IList<SharedProfileMember> Members { get; set; } = [];
}
