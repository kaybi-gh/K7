namespace K7.Server.Application.Features.Users.Commands.UpdateUserPin;

public class UpdateUserPinCommandValidator : AbstractValidator<UpdateUserPinCommand>
{
    public UpdateUserPinCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Pin).MaximumLength(20);
    }
}
