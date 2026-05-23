using K7.Server.Domain.Enums;

namespace K7.Server.Domain.ValueObjects;

public class NotificationCondition : ValueObject
{
    public required string Parameter { get; init; }
    public required NotificationConditionOperator Operator { get; init; }
    public required NotificationConditionValueType ValueType { get; init; }
    public required string Value { get; init; }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Parameter;
        yield return Operator;
        yield return ValueType;
        yield return Value;
    }
}
