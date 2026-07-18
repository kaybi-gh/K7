namespace K7.Server.Application.Features.Users.Commands.RestoreUser;

public class RestoreUserCommandValidator : AbstractValidator<RestoreUserCommand>
{
    public RestoreUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
