namespace K7.Server.Application.Features.Restrictions.Commands.DeleteContentRestrictionProfile;

public class DeleteContentRestrictionProfileCommandValidator : AbstractValidator<DeleteContentRestrictionProfileCommand>
{
    public DeleteContentRestrictionProfileCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
