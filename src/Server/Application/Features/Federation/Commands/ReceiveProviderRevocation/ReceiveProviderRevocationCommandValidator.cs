namespace K7.Server.Application.Features.Federation.Commands.ReceiveProviderRevocation;

public class ReceiveProviderRevocationCommandValidator : AbstractValidator<ReceiveProviderRevocationCommand>
{
    public ReceiveProviderRevocationCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
    }
}
