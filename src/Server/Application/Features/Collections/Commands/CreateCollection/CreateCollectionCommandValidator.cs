namespace K7.Server.Application.Features.Collections.Commands.CreateCollection;

public class CreateCollectionCommandValidator : AbstractValidator<CreateCollectionCommand>
{
    public CreateCollectionCommandValidator()
    {
        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.Description)
            .MaximumLength(2000)
            .When(v => v.Description is not null);

        RuleFor(v => v.VisibilityScope)
            .IsInEnum();

        RuleFor(v => v.MediaType)
            .IsInEnum()
            .When(v => v.MediaType is not null);
    }
}
