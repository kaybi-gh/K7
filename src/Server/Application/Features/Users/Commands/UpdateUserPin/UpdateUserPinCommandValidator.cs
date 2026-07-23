namespace K7.Server.Application.Features.Users.Commands.UpdateUserPin;

public class UpdateUserPinCommandValidator : AbstractValidator<UpdateUserPinCommand>
{
    public UpdateUserPinCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Pin)
            .Matches(@"^\d{4}$")
            .When(x => x.Pin is not null);
    }
}
