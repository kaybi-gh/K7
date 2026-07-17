namespace K7.Server.Application.Features.Federation.Commands.RejectPeerRequest;

public class RejectPeerRequestCommandValidator : AbstractValidator<RejectPeerRequestCommand>
{
    public RejectPeerRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
    }
}
