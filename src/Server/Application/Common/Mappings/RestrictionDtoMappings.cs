using K7.Server.Domain.Entities.Restrictions;
using K7.Shared.Dtos.Restrictions;

namespace K7.Server.Application.Common.Mappings;

public static class RestrictionDtoMappings
{
    extension(ContentRestrictionProfile domain)
    {
        public ContentRestrictionProfileDto ToContentRestrictionProfileDto() => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            Description = domain.Description,
            MatchCondition = domain.MatchCondition,
            Rules = domain.Rules.Select(r => new ContentRestrictionRuleDto
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList(),
            UserCount = domain.Users.Count
        };
    }
}
