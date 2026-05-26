using System.ComponentModel.DataAnnotations.Schema;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Restrictions;

namespace K7.Server.Domain.Entities.Users;

public class User : BaseAuditableEntity
{
    public string? IdentityUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? DisplayName { get; set; }
    public string? PinHash { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? ContentRestrictionProfileId { get; set; }
    public ContentRestrictionProfile? ContentRestrictionProfile { get; set; }
    public IList<UserRating> Ratings { get; set; } = [];
    public IList<Device> Devices { get; set; } = [];
    public IList<UserCapabilityOverride> CapabilityOverrides { get; set; } = [];
    public IList<UserLibraryExclusion> LibraryExclusions { get; set; } = [];
    public IList<UserMediaExclusion> MediaExclusions { get; set; } = [];

    [NotMapped] public string? Email { get; set; }
    [NotMapped] public string? UserName { get; set; }
    [NotMapped] public string Role { get; set; } = "Guest";
}
