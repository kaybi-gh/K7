namespace K7.Server.Application.Features.Users.Commands.DeleteAccount;

public class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).MaximumLength(200);
    }
}
