using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Users.Commands.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => r is Roles.User or Roles.Administrator)
            .WithMessage("Role must be 'User' or 'Administrator'.");
    }
}
