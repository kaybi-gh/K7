namespace K7.Server.Application.Features.IndexedFiles.Commands.GenerateThumbnails;

public class GenerateThumbnailsCommandValidator : AbstractValidator<GenerateThumbnailsCommand>
{
    public GenerateThumbnailsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
