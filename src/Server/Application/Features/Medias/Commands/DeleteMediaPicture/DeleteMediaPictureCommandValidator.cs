namespace K7.Server.Application.Features.Medias.Commands.DeleteMediaPicture;

public class DeleteMediaPictureCommandValidator : AbstractValidator<DeleteMediaPictureCommand>
{
    public DeleteMediaPictureCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.PictureId).NotEmpty();
    }
}
