namespace K7.Server.Application.Features.Federation.Commands.ConfirmPeering;

public class ConfirmPeeringCommandValidator : AbstractValidator<ConfirmPeeringCommand>
{
    public ConfirmPeeringCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ClientSecret).NotEmpty().MaximumLength(500);
        RuleFor(x => x.FederationAssertionSecret).MaximumLength(500);
    }
}
