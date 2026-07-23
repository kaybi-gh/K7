namespace K7.Server.Application.Features.SharedProfiles.Commands.SetSharedProfilePin;

public class SetSharedProfilePinCommandValidator : AbstractValidator<SetSharedProfilePinCommand>
{
    public SetSharedProfilePinCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Pin)
            .Matches(@"^\d{4}$")
            .When(x => x.Pin is not null);
    }
}
