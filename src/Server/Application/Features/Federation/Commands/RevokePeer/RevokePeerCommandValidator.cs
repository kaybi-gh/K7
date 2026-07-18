namespace K7.Server.Application.Features.Federation.Commands.RevokePeer;

public class RevokePeerCommandValidator : AbstractValidator<RevokePeerCommand>
{
    public RevokePeerCommandValidator()
    {
        RuleFor(x => x.PeerId).NotEmpty();
    }
}
