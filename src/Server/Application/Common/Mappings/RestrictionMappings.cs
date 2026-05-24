using K7.Server.Domain.Entities.Restrictions;
using K7.Shared.Dtos.Restrictions;

namespace K7.Server.Application.Common.Mappings;

public static class RestrictionMappings
{
    extension(ContentRestrictionProfile domain)
    {
        public ContentRestrictionProfileDto ToContentRestrictionProfileDto() => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            Description = domain.Description,
            RuleFilter = domain.RuleFilter.ToRuleGroupDto(),
            UserCount = domain.Users.Count
        };
    }
}
