namespace K7.Server.Application.Features.Federation.Commands.ReceivePeerReject;

public class ReceivePeerRejectCommandValidator : AbstractValidator<ReceivePeerRejectCommand>
{
    public ReceivePeerRejectCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
    }
}
