namespace K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;

public class IndexLibraryFilesCommandValidator : AbstractValidator<IndexLibraryFilesCommand>
{
    public IndexLibraryFilesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
