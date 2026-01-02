using K7.Server.Domain.Entities.Ratings;

namespace K7.Server.Domain.Entities.Users;
public class User : BaseAuditableEntity
{
    public required string DisplayName { get; set; }

    public ICollection<Guid> AccessibleLibraryIds { get; set; } = [];
    public ICollection<UserRating> Ratings { get; set; } = [];
}
