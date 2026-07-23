namespace K7.Server.Application.Features.Federation.Commands.CreateFederationStreamSession;

public class CreateFederationStreamSessionCommandValidator : AbstractValidator<CreateFederationStreamSessionCommand>
{
    public CreateFederationStreamSessionCommandValidator()
    {
        RuleFor(x => x.ClientId).MaximumLength(500);
        RuleFor(x => x.Request).NotNull();
    }
}
