using K7.Server.Domain.ValueObjects;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Common.Mappings;

public static class RuleGroupMappings
{
    extension(RuleGroup domain)
    {
        public RuleGroupDto ToRuleGroupDto() => new()
        {
            MatchCondition = domain.MatchCondition,
            Items = domain.Items.Select(MapItem).ToList()
        };
    }

    extension(RuleGroupDto dto)
    {
        public RuleGroup ToRuleGroup() => new()
        {
            MatchCondition = dto.MatchCondition,
            Items = dto.Items.Select(MapItemFromDto).ToList()
        };
    }

    private static RuleGroupItemDto MapItem(RuleGroupItem item) => item switch
    {
        ConditionRuleItem rule => new ConditionRuleItemDto
        {
            Field = rule.Field,
            Operator = rule.Operator,
            Value = rule.Value
        },
        NestedGroupItem group => new NestedGroupItemDto
        {
            MatchCondition = group.MatchCondition,
            Items = group.Items.Select(MapItem).ToList()
        },
        _ => throw new InvalidOperationException($"Unknown RuleGroupItem type: {item.GetType().Name}")
    };

    private static RuleGroupItem MapItemFromDto(RuleGroupItemDto dto) => dto switch
    {
        ConditionRuleItemDto rule => new ConditionRuleItem
        {
            Field = rule.Field,
            Operator = rule.Operator,
            Value = rule.Value
        },
        NestedGroupItemDto group => new NestedGroupItem
        {
            MatchCondition = group.MatchCondition,
            Items = group.Items.Select(MapItemFromDto).ToList()
        },
        _ => throw new InvalidOperationException($"Unknown RuleGroupItemDto type: {dto.GetType().Name}")
    };
}
