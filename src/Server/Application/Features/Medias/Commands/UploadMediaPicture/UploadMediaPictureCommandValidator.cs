namespace K7.Server.Application.Features.Medias.Commands.UploadMediaPicture;

public class UploadMediaPictureCommandValidator : AbstractValidator<UploadMediaPictureCommand>
{
    public UploadMediaPictureCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.PictureType).IsInEnum();
        RuleFor(x => x.FileStream).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
    }
}
