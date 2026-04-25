using K7.Server.Domain.Common;

namespace K7.Server.Domain.Entities.Users;

public class UserMediaExclusion : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid MediaId { get; set; }
    public bool IsAdminExcluded { get; set; }
    public bool IsSelfExcluded { get; set; }
}
