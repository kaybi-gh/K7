namespace K7.Server.Application.Features.IndexedFiles.Commands.RefreshIndexedFile;

public class RefreshIndexedFileCommandValidator : AbstractValidator<RefreshIndexedFileCommand>
{
    public RefreshIndexedFileCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
