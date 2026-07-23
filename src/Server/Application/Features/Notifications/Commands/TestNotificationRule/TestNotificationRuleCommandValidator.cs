namespace K7.Server.Application.Features.Notifications.Commands.TestNotificationRule;

public class TestNotificationRuleCommandValidator : AbstractValidator<TestNotificationRuleCommand>
{
    public TestNotificationRuleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
