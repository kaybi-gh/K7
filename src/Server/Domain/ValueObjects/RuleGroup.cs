using System.Text.Json.Serialization;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.ValueObjects;

public class RuleGroup
{
    public RuleMatchCondition MatchCondition { get; set; }

    public List<RuleGroupItem> Items { get; set; } = [];
}

[JsonDerivedType(typeof(ConditionRuleItem), "rule")]
[JsonDerivedType(typeof(NestedGroupItem), "group")]
public abstract class RuleGroupItem;

public class ConditionRuleItem : RuleGroupItem
{
    public required string Field { get; set; }
    public required RuleOperator Operator { get; set; }
    public string? Value { get; set; }
}

public class NestedGroupItem : RuleGroupItem
{
    public RuleMatchCondition MatchCondition { get; set; }
    public List<RuleGroupItem> Items { get; set; } = [];
}
