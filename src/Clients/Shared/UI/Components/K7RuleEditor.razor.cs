using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class K7RuleEditor : ComponentBase
{
    [Parameter] public RuleGroupDto Value { get; set; } = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    [Parameter] public EventCallback<RuleGroupDto> ValueChanged { get; set; }
    [Parameter] public IReadOnlyList<RuleFieldDescriptorDto> FieldDescriptors { get; set; } = [];
    [Parameter] public string Class { get; set; } = "";

    private EditGroup _root = new();

    protected override void OnParametersSet()
    {
        _root = FromDto(Value);
    }

    private RuleFieldDescriptorDto? GetDescriptor(string fieldName) =>
        FieldDescriptors.FirstOrDefault(f => f.FieldName == fieldName);

    private static bool IsUnaryOperator(RuleOperator op) =>
        op is RuleOperator.IsEmpty or RuleOperator.IsNotEmpty;

    private static string GetInputType(RuleFieldDescriptorDto? descriptor) => descriptor?.ValueType switch
    {
        RuleFieldValueType.Number => "number",
        RuleFieldValueType.Date => "date",
        _ => "text"
    };

    private string GetOperatorLabel(RuleOperator op) => op switch
    {
        RuleOperator.Equals => L["OpEquals"],
        RuleOperator.NotEquals => L["OpNotEquals"],
        RuleOperator.Contains => L["OpContains"],
        RuleOperator.NotContains => L["OpNotContains"],
        RuleOperator.GreaterThan => L["OpGreaterThan"],
        RuleOperator.LessThan => L["OpLessThan"],
        RuleOperator.GreaterThanOrEqual => L["OpGreaterThanOrEqual"],
        RuleOperator.LessThanOrEqual => L["OpLessThanOrEqual"],
        RuleOperator.BeginsWith => L["OpBeginsWith"],
        RuleOperator.EndsWith => L["OpEndsWith"],
        RuleOperator.InLast => L["OpInLast"],
        RuleOperator.IsEmpty => L["OpIsEmpty"],
        RuleOperator.IsNotEmpty => L["OpIsNotEmpty"],
        _ => op.ToString()
    };

    private void OnMatchConditionChanged(EditGroup group, RuleMatchCondition value)
    {
        group.MatchCondition = value;
        NotifyChanged();
    }

    private void OnFieldChanged(EditGroup group, int index, string field)
    {
        var rule = (EditRule)group.Items[index];
        rule.Field = field;
        var descriptor = GetDescriptor(field);
        if (descriptor is not null && !descriptor.Operators.Contains(rule.Operator))
            rule.Operator = descriptor.Operators[0];
        rule.Value = null;
        NotifyChanged();
    }

    private void OnOperatorChanged(EditGroup group, int index, RuleOperator op)
    {
        var rule = (EditRule)group.Items[index];
        rule.Operator = op;
        NotifyChanged();
    }

    private void OnValueChanged(EditGroup group, int index, string? value)
    {
        var rule = (EditRule)group.Items[index];
        rule.Value = value;
        NotifyChanged();
    }

    private void AddRule(EditGroup group)
    {
        var defaultField = FieldDescriptors.FirstOrDefault();
        group.Items.Add(new EditRule
        {
            Field = defaultField?.FieldName ?? "",
            Operator = defaultField?.Operators.FirstOrDefault() ?? RuleOperator.Equals
        });
        NotifyChanged();
    }

    private void AddGroup(EditGroup group)
    {
        group.Items.Add(new EditGroup { MatchCondition = RuleMatchCondition.All });
        NotifyChanged();
    }

    private void RemoveItem(EditGroup group, int index)
    {
        group.Items.RemoveAt(index);
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Value = ToDto(_root);
        ValueChanged.InvokeAsync(Value);
    }

    private static EditGroup FromDto(RuleGroupDto dto) => new()
    {
        MatchCondition = dto.MatchCondition,
        Items = dto.Items.Select<RuleGroupItemDto, EditItem>(item => item switch
        {
            ConditionRuleItemDto r => new EditRule { Field = r.Field, Operator = r.Operator, Value = r.Value },
            NestedGroupItemDto g => FromDto(new RuleGroupDto { MatchCondition = g.MatchCondition, Items = g.Items }),
            _ => throw new InvalidOperationException()
        }).ToList()
    };

    private static RuleGroupDto ToDto(EditGroup group) => new()
    {
        MatchCondition = group.MatchCondition,
        Items = group.Items.Select<EditItem, RuleGroupItemDto>(item => item switch
        {
            EditRule r => new ConditionRuleItemDto { Field = r.Field, Operator = r.Operator, Value = r.Value },
            EditGroup g => new NestedGroupItemDto { MatchCondition = g.MatchCondition, Items = ToDto(g).Items },
            _ => throw new InvalidOperationException()
        }).ToList()
    };

    internal abstract class EditItem;

    internal sealed class EditRule : EditItem
    {
        public string Field { get; set; } = "";
        public RuleOperator Operator { get; set; }
        public string? Value { get; set; }
    }

    internal sealed class EditGroup : EditItem
    {
        public RuleMatchCondition MatchCondition { get; set; }
        public List<EditItem> Items { get; set; } = [];
    }
}
