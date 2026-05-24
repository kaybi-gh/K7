using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Notifications.Commands.UpdateNotificationRule;

public class UpdateNotificationRuleCommandValidator : AbstractValidator<UpdateNotificationRuleCommand>
{
    public UpdateNotificationRuleCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();

        RuleFor(v => v.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.ProviderType)
            .NotEmpty()
            .Must(v => Enum.TryParse<NotificationProviderType>(v, ignoreCase: true, out _))
                .WithMessage("Invalid provider type.");

        RuleFor(v => v.EventTypeNames)
            .NotEmpty()
            .Must(v => v.Count <= 50)
                .WithMessage("Maximum 50 event types per rule.");

        RuleFor(v => v.ProviderConfig)
            .NotEmpty();
    }
}
