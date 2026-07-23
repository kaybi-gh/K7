namespace K7.Server.Application.Features.ApiKeys.Commands.CreateApiKey;

public class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scope).IsInEnum();
    }
}
