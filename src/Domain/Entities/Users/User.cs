using MediaServer.Domain.Entities.Ratings;

namespace MediaServer.Domain.Entities.Users;
public class User : BaseAuditableEntity
{
    public required string Username { get; set; }
    public required string AuthenticationProviderId { get; set; }

    public virtual IEnumerable<UserRating>? Ratings { get; set; }
}
