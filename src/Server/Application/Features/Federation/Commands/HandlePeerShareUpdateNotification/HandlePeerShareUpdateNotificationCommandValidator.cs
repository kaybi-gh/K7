namespace K7.Server.Application.Features.Federation.Commands.HandlePeerShareUpdateNotification;

public class HandlePeerShareUpdateNotificationCommandValidator : AbstractValidator<HandlePeerShareUpdateNotificationCommand>
{
    public HandlePeerShareUpdateNotificationCommandValidator()
    {
        RuleFor(x => x.ClientId).MaximumLength(500);
        RuleFor(x => x.Request).NotNull();
    }
}
