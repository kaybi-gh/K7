namespace K7.Server.Application.Features.Persons.Commands.DeletePersonPicture;

public class DeletePersonPictureCommandValidator : AbstractValidator<DeletePersonPictureCommand>
{
    public DeletePersonPictureCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty();
    }
}
