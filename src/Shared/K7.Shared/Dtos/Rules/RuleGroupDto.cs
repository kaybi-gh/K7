using System.Text.Json.Serialization;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Rules;

public sealed record RuleGroupDto
{
    public RuleMatchCondition MatchCondition { get; init; }
    public IReadOnlyList<RuleGroupItemDto> Items { get; init; } = [];
}

[JsonDerivedType(typeof(ConditionRuleItemDto), "rule")]
[JsonDerivedType(typeof(NestedGroupItemDto), "group")]
public abstract record RuleGroupItemDto;

public sealed record ConditionRuleItemDto : RuleGroupItemDto
{
    public required string Field { get; init; }
    public required RuleOperator Operator { get; init; }
    public string? Value { get; init; }
}

public sealed record NestedGroupItemDto : RuleGroupItemDto
{
    public RuleMatchCondition MatchCondition { get; init; }
    public IReadOnlyList<RuleGroupItemDto> Items { get; init; } = [];
}
