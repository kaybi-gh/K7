namespace K7.Server.Application.Features.Users.Commands.VerifyUserPin;

public class VerifyUserPinCommandValidator : AbstractValidator<VerifyUserPinCommand>
{
    public VerifyUserPinCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Pin).NotEmpty().MaximumLength(20);
    }
}
