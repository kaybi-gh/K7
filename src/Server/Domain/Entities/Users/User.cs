using System.ComponentModel.DataAnnotations.Schema;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Ratings;

namespace K7.Server.Domain.Entities.Users;
public class User : BaseAuditableEntity
{
    public string? IdentityUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<UserRating> Ratings { get; set; } = [];
    public ICollection<Device> Devices { get; set; } = [];
    public ICollection<UserCapabilityOverride> CapabilityOverrides { get; set; } = [];
    public ICollection<UserLibraryExclusion> LibraryExclusions { get; set; } = [];

    [NotMapped] public string? Email { get; set; }
    [NotMapped] public string? UserName { get; set; }
    [NotMapped] public string Role { get; set; } = "Guest";
}
