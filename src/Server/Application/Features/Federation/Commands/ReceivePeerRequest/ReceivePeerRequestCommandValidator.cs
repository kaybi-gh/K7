namespace K7.Server.Application.Features.Federation.Commands.ReceivePeerRequest;

public class ReceivePeerRequestCommandValidator : AbstractValidator<ReceivePeerRequestCommand>
{
    public ReceivePeerRequestCommandValidator()
    {
        RuleFor(x => x.RequesterUrl).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.RequesterName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Token).NotEmpty().MaximumLength(500);
    }
}
