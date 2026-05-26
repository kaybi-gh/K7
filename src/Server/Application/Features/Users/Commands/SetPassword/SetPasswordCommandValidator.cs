namespace K7.Server.Application.Features.Users.Commands.SetPassword;

public class SetPasswordCommandValidator : AbstractValidator<SetPasswordCommand>
{
    public SetPasswordCommandValidator()
    {
        RuleFor(v => v.NewPassword).NotEmpty().MinimumLength(6);
    }
}
