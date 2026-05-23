using System.Globalization;
using K7.Server.Domain.Enums;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Application.Features.Notifications.Services;

public class NotificationConditionEvaluator
{
    public bool Evaluate(
        IReadOnlyList<NotificationCondition> conditions,
        string? conditionsLogic,
        IReadOnlyDictionary<string, object?> eventData)
    {
        if (conditions.Count == 0)
            return true;

        var results = new bool[conditions.Count];
        for (var i = 0; i < conditions.Count; i++)
        {
            results[i] = EvaluateCondition(conditions[i], eventData);
        }

        if (string.IsNullOrWhiteSpace(conditionsLogic))
            return results.All(r => r);

        return EvaluateLogic(conditionsLogic, results);
    }

    private static bool EvaluateCondition(NotificationCondition condition, IReadOnlyDictionary<string, object?> eventData)
    {
        if (!eventData.TryGetValue(condition.Parameter, out var rawValue))
            return false;

        var actual = rawValue?.ToString() ?? string.Empty;
        var expected = condition.Value;

        return condition.Operator switch
        {
            NotificationConditionOperator.Is => CompareEqual(actual, expected, condition.ValueType),
            NotificationConditionOperator.IsNot => !CompareEqual(actual, expected, condition.ValueType),
            NotificationConditionOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            NotificationConditionOperator.DoesNotContain => !actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            NotificationConditionOperator.GreaterThan => CompareNumeric(actual, expected) > 0,
            NotificationConditionOperator.LessThan => CompareNumeric(actual, expected) < 0,
            NotificationConditionOperator.BeginsWith => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            NotificationConditionOperator.EndsWith => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool CompareEqual(string actual, string expected, NotificationConditionValueType valueType)
    {
        return valueType switch
        {
            NotificationConditionValueType.Int =>
                long.TryParse(actual, CultureInfo.InvariantCulture, out var a) &&
                long.TryParse(expected, CultureInfo.InvariantCulture, out var b) &&
                a == b,
            NotificationConditionValueType.Float =>
                double.TryParse(actual, CultureInfo.InvariantCulture, out var af) &&
                double.TryParse(expected, CultureInfo.InvariantCulture, out var bf) &&
                Math.Abs(af - bf) < 0.0001,
            _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
        };
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

    private static bool EvaluateLogic(string logic, bool[] results)
    {
        // Simple parser for logic like "1 and 2", "1 or 2", "1 and (2 or 3)"
        var tokens = Tokenize(logic);
        var index = 0;
        return ParseExpression(tokens, results, ref index);
    }

    private static List<string> Tokenize(string logic)
    {
        var tokens = new List<string>();
        var current = "";

        foreach (var ch in logic)
        {
            if (ch is '(' or ')')
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.Trim());
                    current = "";
                }
                tokens.Add(ch.ToString());
            }
            else if (ch == ' ')
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.Trim());
                    current = "";
                }
            }
            else
            {
                current += ch;
            }
        }

        if (current.Length > 0)
            tokens.Add(current.Trim());

        return tokens;
    }

    private static bool ParseExpression(List<string> tokens, bool[] results, ref int index)
    {
        var left = ParsePrimary(tokens, results, ref index);

        while (index < tokens.Count)
        {
            var token = tokens[index].ToLowerInvariant();
            if (token == "and")
            {
                index++;
                var right = ParsePrimary(tokens, results, ref index);
                left = left && right;
            }
            else if (token == "or")
            {
                index++;
                var right = ParsePrimary(tokens, results, ref index);
                left = left || right;
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private static bool ParsePrimary(List<string> tokens, bool[] results, ref int index)
    {
        if (index >= tokens.Count)
            return true;

        var token = tokens[index];

        if (token == "(")
        {
            index++;
            var result = ParseExpression(tokens, results, ref index);
            if (index < tokens.Count && tokens[index] == ")")
                index++;
            return result;
        }

        if (int.TryParse(token, out var conditionIndex) && conditionIndex >= 1 && conditionIndex <= results.Length)
        {
            index++;
            return results[conditionIndex - 1];
        }

        index++;
        return true;
    }
}
