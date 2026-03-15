using K7.Server.Domain.Common;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Users;

public class UserCapabilityOverride : BaseEntity
{
    public Guid UserId { get; set; }
    public Capability Capability { get; set; }
    public bool Enabled { get; set; }
}
