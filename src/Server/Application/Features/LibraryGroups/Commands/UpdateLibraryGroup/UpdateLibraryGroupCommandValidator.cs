namespace K7.Server.Application.Features.LibraryGroups.Commands.UpdateLibraryGroup;

public class UpdateLibraryGroupCommandValidator : AbstractValidator<UpdateLibraryGroupCommand>
{
    public UpdateLibraryGroupCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Icon).MaximumLength(200);
        RuleFor(x => x.CardColor).MaximumLength(50);
    }
}
