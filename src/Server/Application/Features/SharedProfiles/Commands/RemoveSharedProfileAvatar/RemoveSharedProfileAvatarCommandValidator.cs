namespace K7.Server.Application.Features.SharedProfiles.Commands.RemoveSharedProfileAvatar;

public class RemoveSharedProfileAvatarCommandValidator : AbstractValidator<RemoveSharedProfileAvatarCommand>
{
    public RemoveSharedProfileAvatarCommandValidator()
    {
        RuleFor(x => x.SharedProfileId).NotEmpty();
    }
}
