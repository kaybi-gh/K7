namespace K7.Server.Application.Features.SharedProfiles.Commands.UploadSharedProfileAvatar;

public class UploadSharedProfileAvatarCommandValidator : AbstractValidator<UploadSharedProfileAvatarCommand>
{
    public UploadSharedProfileAvatarCommandValidator()
    {
        RuleFor(x => x.SharedProfileId).NotEmpty();
        RuleFor(x => x.FileStream).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
    }
}
