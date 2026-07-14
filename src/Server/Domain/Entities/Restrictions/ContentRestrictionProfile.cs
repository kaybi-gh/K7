using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Domain.Entities.Restrictions;

public class ContentRestrictionProfile : BaseAuditableEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public RuleGroup RuleFilter { get; set; } = new() { MatchCondition = RuleMatchCondition.Any };
    public ICollection<User> Users { get; set; } = [];
}
