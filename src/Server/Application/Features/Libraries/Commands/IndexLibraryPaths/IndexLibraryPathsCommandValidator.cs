namespace K7.Server.Application.Features.Libraries.Commands.IndexLibraryPaths;

public class IndexLibraryPathsCommandValidator : AbstractValidator<IndexLibraryPathsCommand>
{
    public IndexLibraryPathsCommandValidator()
    {
        RuleFor(x => x.LibraryId).NotEmpty();
        RuleFor(x => x.Paths).NotNull();
        RuleForEach(x => x.Paths).NotEmpty();
    }
}
