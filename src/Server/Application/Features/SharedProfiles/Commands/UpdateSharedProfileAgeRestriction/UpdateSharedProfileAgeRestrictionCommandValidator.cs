namespace K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileAgeRestriction;

public class UpdateSharedProfileAgeRestrictionCommandValidator : AbstractValidator<UpdateSharedProfileAgeRestrictionCommand>
{
    public UpdateSharedProfileAgeRestrictionCommandValidator()
    {
        RuleFor(x => x.SharedProfileId).NotEmpty();
        RuleFor(x => x.DateOfBirth)
            .NotNull()
            .When(x => x.Enabled);
        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue);
    }
}
