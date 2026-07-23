namespace K7.Server.Application.Features.Federation.Commands.CreateVirtualUser;

public class CreateVirtualUserCommandValidator : AbstractValidator<CreateVirtualUserCommand>
{
    public CreateVirtualUserCommandValidator()
    {
        RuleFor(x => x.PeerServerId).NotEmpty();
        RuleFor(x => x.OriginUserId).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}
