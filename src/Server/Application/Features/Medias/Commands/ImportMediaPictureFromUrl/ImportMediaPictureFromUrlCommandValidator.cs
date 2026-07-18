namespace K7.Server.Application.Features.Medias.Commands.ImportMediaPictureFromUrl;

public class ImportMediaPictureFromUrlCommandValidator : AbstractValidator<ImportMediaPictureFromUrlCommand>
{
    public ImportMediaPictureFromUrlCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.PictureType).IsInEnum();
    }
}
