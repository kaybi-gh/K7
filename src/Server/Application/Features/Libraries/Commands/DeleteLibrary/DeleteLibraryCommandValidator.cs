namespace K7.Server.Application.Features.Libraries.Commands.DeleteLibrary;

public class DeleteLibraryCommandValidator : AbstractValidator<DeleteLibraryCommand>
{
    public DeleteLibraryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
