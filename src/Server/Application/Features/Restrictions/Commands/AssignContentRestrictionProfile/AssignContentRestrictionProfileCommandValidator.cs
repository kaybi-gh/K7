namespace K7.Server.Application.Features.Restrictions.Commands.AssignContentRestrictionProfile;

public class AssignContentRestrictionProfileCommandValidator : AbstractValidator<AssignContentRestrictionProfileCommand>
{
    public AssignContentRestrictionProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ProfileId).NotEqual(Guid.Empty).When(x => x.ProfileId.HasValue);
    }
}
