namespace K7.Server.Application.Features.Restrictions.Commands.CreateContentRestrictionProfile;

public class CreateContentRestrictionProfileCommandValidator
    : AbstractValidator<CreateContentRestrictionProfileCommand>
{
    public CreateContentRestrictionProfileCommandValidator()
    {
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
