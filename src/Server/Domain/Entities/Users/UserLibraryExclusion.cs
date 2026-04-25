using K7.Server.Domain.Common;

namespace K7.Server.Domain.Entities.Users;

public class UserLibraryExclusion : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid LibraryId { get; set; }
    public bool IsAdminExcluded { get; set; }
    public bool IsSelfExcluded { get; set; }
}
