namespace K7.Server.Application.Features.Users.Commands.RemovePassword;

public class RemovePasswordCommandValidator : AbstractValidator<RemovePasswordCommand>
{
    public RemovePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(200);
    }
}
