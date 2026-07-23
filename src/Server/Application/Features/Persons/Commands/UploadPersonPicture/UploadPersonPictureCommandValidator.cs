namespace K7.Server.Application.Features.Persons.Commands.UploadPersonPicture;

public class UploadPersonPictureCommandValidator : AbstractValidator<UploadPersonPictureCommand>
{
    public UploadPersonPictureCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.PictureType).IsInEnum();
        RuleFor(x => x.FileStream).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
    }
}
