namespace K7.Server.Application.Features.Libraries.Commands.DeleteIndexedFile;

public class DeleteIndexedFileCommandValidator : AbstractValidator<DeleteIndexedFileCommand>
{
    public DeleteIndexedFileCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
