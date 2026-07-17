using K7.Server.Domain.Common;

namespace K7.Server.Domain.Entities;

public class SyncPlayInvite : BaseEntity
{
    public required string Token { get; set; }
    public Guid GroupId { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
