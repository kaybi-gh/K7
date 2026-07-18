namespace K7.Server.Application.Features.ApiKeys.Commands.RevokeApiKey;

public class RevokeApiKeyCommandValidator : AbstractValidator<RevokeApiKeyCommand>
{
    public RevokeApiKeyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
