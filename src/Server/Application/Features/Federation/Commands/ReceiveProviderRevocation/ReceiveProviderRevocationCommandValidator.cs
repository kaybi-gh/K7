namespace K7.Server.Application.Features.Federation.Commands.ReceiveProviderRevocation;

public class ReceiveProviderRevocationCommandValidator : AbstractValidator<ReceiveProviderRevocationCommand>
{
    public ReceiveProviderRevocationCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.ProviderUrl).NotEmpty();
            RuleFor(x => x.Request.Signature).NotEmpty();
            RuleFor(x => x.Request.Expires).GreaterThan(0);
        });
    }
}
