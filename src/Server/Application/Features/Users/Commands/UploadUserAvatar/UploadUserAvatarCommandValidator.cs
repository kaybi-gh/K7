namespace K7.Server.Application.Features.Users.Commands.UploadUserAvatar;

public class UploadUserAvatarCommandValidator : AbstractValidator<UploadUserAvatarCommand>
{
    public UploadUserAvatarCommandValidator()
    {
        RuleFor(x => x.FileStream).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
    }
}
