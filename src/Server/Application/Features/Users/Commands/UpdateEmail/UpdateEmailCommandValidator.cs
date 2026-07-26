namespace K7.Server.Application.Features.Users.Commands.UpdateEmail;

public class UpdateEmailCommandValidator : AbstractValidator<UpdateEmailCommand>
{
    public UpdateEmailCommandValidator()
    {
        RuleFor(v => v.Email)
            .EmailAddress()
            .When(v => !string.IsNullOrWhiteSpace(v.Email));
        RuleFor(v => v.CurrentPassword).NotEmpty();
    }
}
