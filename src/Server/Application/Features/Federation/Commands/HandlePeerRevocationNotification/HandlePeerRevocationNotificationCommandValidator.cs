namespace K7.Server.Application.Features.Federation.Commands.HandlePeerRevocationNotification;

public class HandlePeerRevocationNotificationCommandValidator : AbstractValidator<HandlePeerRevocationNotificationCommand>
{
    public HandlePeerRevocationNotificationCommandValidator()
    {
        RuleFor(x => x.ClientId).MaximumLength(500);
    }
}
