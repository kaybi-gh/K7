namespace K7.Server.Application.Features.ClientAppPasswords.Commands.CreateClientAppPassword;

public class CreateClientAppPasswordCommandValidator : AbstractValidator<CreateClientAppPasswordCommand>
{
    public CreateClientAppPasswordCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
