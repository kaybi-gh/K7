namespace K7.Server.Application.Features.Federation.Commands.HandlePeerMediaNotification;

public class HandlePeerMediaNotificationCommandValidator : AbstractValidator<HandlePeerMediaNotificationCommand>
{
    public HandlePeerMediaNotificationCommandValidator()
    {
        RuleFor(x => x.ClientId).MaximumLength(500);
        RuleFor(x => x.Request).NotNull();
    }
}
