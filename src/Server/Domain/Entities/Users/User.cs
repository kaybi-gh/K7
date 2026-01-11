using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Ratings;

namespace K7.Server.Domain.Entities.Users;
public class User : BaseAuditableEntity
{
    public string? IdentityUserId { get; set; }
    public ICollection<Guid> AccessibleLibraryIds { get; set; } = [];
    public ICollection<UserRating> Ratings { get; set; } = [];
    public ICollection<Device> Devices { get; set; } = [];
}
