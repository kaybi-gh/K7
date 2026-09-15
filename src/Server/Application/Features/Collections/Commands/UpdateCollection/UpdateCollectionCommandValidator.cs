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

        RuleFor(v => v.MediaType)
            .NotNull()
            .IsInEnum()
            .When(v => v.RuleFilter is not null);

        RuleFor(v => v.OrderBy)
            .IsInEnum()
            .When(v => v.RuleFilter is not null);

        RuleFor(v => v.Limit)
            .GreaterThan(0)
            .LessThanOrEqualTo(1000)
            .When(v => v.Limit is not null);

        RuleFor(v => v.LibraryGroupId)
            .NotEmpty()
            .When(v => v.LibraryGroupId is not null);

        RuleFor(v => v.RuleFilter)
            .NotNull()
            .When(v => v.LibraryGroupId is not null);
    }
}
