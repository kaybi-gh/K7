namespace K7.Server.Application.Features.Restrictions.Commands.UpdateUserAgeRestriction;

public class UpdateUserAgeRestrictionCommandValidator : AbstractValidator<UpdateUserAgeRestrictionCommand>
{
    public UpdateUserAgeRestrictionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DateOfBirth)
            .NotNull()
            .When(x => x.Enabled);
        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue);
    }
}
