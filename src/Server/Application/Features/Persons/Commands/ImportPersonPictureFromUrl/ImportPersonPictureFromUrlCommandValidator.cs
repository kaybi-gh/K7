namespace K7.Server.Application.Features.Persons.Commands.ImportPersonPictureFromUrl;

public class ImportPersonPictureFromUrlCommandValidator : AbstractValidator<ImportPersonPictureFromUrlCommand>
{
    public ImportPersonPictureFromUrlCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.PictureType).IsInEnum();
    }
}
