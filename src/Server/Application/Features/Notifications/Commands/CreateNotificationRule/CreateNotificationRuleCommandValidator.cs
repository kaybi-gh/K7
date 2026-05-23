using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Notifications.Commands.CreateNotificationRule;

public class CreateNotificationRuleCommandValidator : AbstractValidator<CreateNotificationRuleCommand>
{
    public CreateNotificationRuleCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.ProviderType)
            .NotEmpty()
            .Must(v => Enum.TryParse<NotificationProviderType>(v, ignoreCase: true, out _))
                .WithMessage("Invalid provider type.");

        RuleFor(v => v.EventTypeName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.ProviderConfig)
            .NotEmpty();
    }
}
