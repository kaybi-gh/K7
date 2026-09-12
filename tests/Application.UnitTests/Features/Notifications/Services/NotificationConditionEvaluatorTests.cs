using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Application.UnitTests.Features.Notifications.Services;

[TestFixture]
public class NotificationConditionEvaluatorTests
{
    private readonly NotificationConditionEvaluator _evaluator = new();

    [Test]
    public void Evaluate_ShouldReturnTrue_WhenFilterIsEmpty()
    {
        _evaluator.Evaluate(null, new Dictionary<string, object?>()).Should().BeTrue();
    }

    [Test]
    public void Evaluate_ShouldSkipMissingField_WhenOperatorIsNotEmptinessCheck()
    {
        var filter = new RuleGroup
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new ConditionRuleItem
                {
                    Field = "User.Origin",
                    Operator = RuleOperator.Equals,
                    Value = "Admin"
                }
            ]
        };

        _evaluator.Evaluate(filter, new Dictionary<string, object?> { ["User.Name"] = "alice" })
            .Should().BeTrue();
    }

    [Test]
    public void Evaluate_ShouldTreatMissingFieldAsEmpty_WhenOperatorIsEmpty()
    {
        var filter = new RuleGroup
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new ConditionRuleItem
                {
                    Field = "User.Origin",
                    Operator = RuleOperator.IsEmpty,
                    Value = null
                }
            ]
        };

        _evaluator.Evaluate(filter, new Dictionary<string, object?>()).Should().BeTrue();
    }

    [Test]
    public void Evaluate_ShouldHonorGreaterThanOrEqual()
    {
        var filter = new RuleGroup
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new ConditionRuleItem
                {
                    Field = "Session.ProgressPercent",
                    Operator = RuleOperator.GreaterThanOrEqual,
                    Value = "50"
                }
            ]
        };

        _evaluator.Evaluate(filter, new Dictionary<string, object?> { ["Session.ProgressPercent"] = 50 })
            .Should().BeTrue();
        _evaluator.Evaluate(filter, new Dictionary<string, object?> { ["Session.ProgressPercent"] = 49 })
            .Should().BeFalse();
    }

    [Test]
    public void Evaluate_ShouldHonorInLast_WhenTimestampIsRecent()
    {
        var filter = new RuleGroup
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new ConditionRuleItem
                {
                    Field = "Current.Timestamp",
                    Operator = RuleOperator.InLast,
                    Value = "2"
                }
            ]
        };

        var recent = DateTimeOffset.UtcNow.AddHours(-1).ToString("o");
        var old = DateTimeOffset.UtcNow.AddDays(-5).ToString("o");

        _evaluator.Evaluate(filter, new Dictionary<string, object?> { ["Current.Timestamp"] = recent })
            .Should().BeTrue();
        _evaluator.Evaluate(filter, new Dictionary<string, object?> { ["Current.Timestamp"] = old })
            .Should().BeFalse();
    }
}
