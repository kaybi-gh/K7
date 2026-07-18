namespace K7.Server.Application.Features.IndexedFiles.Commands.ExtractChapters;

public class ExtractChaptersCommandValidator : AbstractValidator<ExtractChaptersCommand>
{
    public ExtractChaptersCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
