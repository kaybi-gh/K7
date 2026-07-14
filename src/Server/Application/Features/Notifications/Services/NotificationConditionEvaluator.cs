using System.Globalization;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Application.Features.Notifications.Services;

public class NotificationConditionEvaluator
{
    public bool Evaluate(
        RuleGroup? ruleFilter,
        IReadOnlyDictionary<string, object?> eventData)
    {
        if (ruleFilter is null || ruleFilter.Items.Count == 0)
            return true;

        return EvaluateGroup(ruleFilter, eventData);
    }

    private static bool EvaluateGroup(RuleGroup group, IReadOnlyDictionary<string, object?> eventData)
    {
        if (group.Items.Count == 0)
            return true;

        var results = group.Items.Select(item => item switch
        {
            ConditionRuleItem rule => EvaluateRule(rule, eventData),
            NestedGroupItem nested => EvaluateGroup(
                new RuleGroup { MatchCondition = nested.MatchCondition, Items = nested.Items },
                eventData),
            _ => true
        });

        return group.MatchCondition == RuleMatchCondition.All
            ? results.All(r => r)
            : results.Any(r => r);
    }

    private static bool EvaluateRule(ConditionRuleItem rule, IReadOnlyDictionary<string, object?> eventData)
    {
        if (!eventData.TryGetValue(rule.Field, out var rawValue))
        {
            return rule.Operator is RuleOperator.IsEmpty;
        }

        var actual = rawValue?.ToString() ?? string.Empty;
        var expected = rule.Value ?? string.Empty;

        return rule.Operator switch
        {
            RuleOperator.Equals => CompareEqual(actual, expected),
            RuleOperator.NotEquals => !CompareEqual(actual, expected),
            RuleOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.NotContains => !actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.GreaterThan => CompareNumeric(actual, expected) > 0,
            RuleOperator.LessThan => CompareNumeric(actual, expected) < 0,
            RuleOperator.GreaterThanOrEqual => CompareNumeric(actual, expected) >= 0,
            RuleOperator.LessThanOrEqual => CompareNumeric(actual, expected) <= 0,
            RuleOperator.BeginsWith => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.EndsWith => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.IsEmpty => string.IsNullOrEmpty(actual),
            RuleOperator.IsNotEmpty => !string.IsNullOrEmpty(actual),
            _ => false
        };
    }

    private static bool CompareEqual(string actual, string expected)
    {
        if (long.TryParse(actual, CultureInfo.InvariantCulture, out var ai) &&
            long.TryParse(expected, CultureInfo.InvariantCulture, out var bi))
            return ai == bi;

        if (double.TryParse(actual, CultureInfo.InvariantCulture, out var af) &&
            double.TryParse(expected, CultureInfo.InvariantCulture, out var bf))
            return Math.Abs(af - bf) < 0.0001;

        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareNumeric(string actual, string expected)
    {
        if (double.TryParse(actual, CultureInfo.InvariantCulture, out var a) &&
            double.TryParse(expected, CultureInfo.InvariantCulture, out var b))
        {
            return a.CompareTo(b);
        }

        return string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
