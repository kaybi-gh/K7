namespace K7.Server.Application.Features.LibraryGroups.Commands.DeleteLibraryGroup;

public class DeleteLibraryGroupCommandValidator : AbstractValidator<DeleteLibraryGroupCommand>
{
    public DeleteLibraryGroupCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
