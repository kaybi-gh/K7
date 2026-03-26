using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Restrictions;

public class ContentRestrictionProfile : BaseAuditableEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public RestrictionMatchCondition MatchCondition { get; set; } = RestrictionMatchCondition.Any;
    public IList<ContentRestrictionRule> Rules { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}
