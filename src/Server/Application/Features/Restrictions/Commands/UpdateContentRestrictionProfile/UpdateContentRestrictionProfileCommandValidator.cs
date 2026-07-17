namespace K7.Server.Application.Features.Restrictions.Commands.UpdateContentRestrictionProfile;

public class UpdateContentRestrictionProfileCommandValidator
    : AbstractValidator<UpdateContentRestrictionProfileCommand>
{
    public UpdateContentRestrictionProfileCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();

        RuleFor(v => v.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.Description)
            .MaximumLength(2000)
            .When(v => v.Description is not null);

        RuleFor(v => v.RuleFilter)
            .NotNull();
    }
}
