namespace K7.Server.Application.Features.SharedProfiles.Commands.VerifySharedProfilePin;

public class VerifySharedProfilePinCommandValidator : AbstractValidator<VerifySharedProfilePinCommand>
{
    public VerifySharedProfilePinCommandValidator()
    {
        RuleFor(x => x.SharedProfileId).NotEmpty();
        RuleFor(x => x.Pin).NotEmpty().MaximumLength(20);
    }
}
