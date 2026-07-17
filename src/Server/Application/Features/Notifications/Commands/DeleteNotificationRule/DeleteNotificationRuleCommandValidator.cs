namespace K7.Server.Application.Features.Notifications.Commands.DeleteNotificationRule;

public class DeleteNotificationRuleCommandValidator : AbstractValidator<DeleteNotificationRuleCommand>
{
    public DeleteNotificationRuleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
