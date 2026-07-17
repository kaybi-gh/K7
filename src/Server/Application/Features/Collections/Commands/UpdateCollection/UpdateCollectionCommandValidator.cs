namespace K7.Server.Application.Features.Collections.Commands.UpdateCollection;

public class UpdateCollectionCommandValidator : AbstractValidator<UpdateCollectionCommand>
{
    public UpdateCollectionCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();

        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.Description)
            .MaximumLength(2000)
            .When(v => v.Description is not null);

        RuleFor(v => v.VisibilityScope)
            .IsInEnum();
    }
}
