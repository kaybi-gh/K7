namespace K7.Server.Application.Features.VideoPlayerSettings.Commands.UpdateUserVideoPlayerSettings;

public class UpdateUserVideoPlayerSettingsCommandValidator : AbstractValidator<UpdateUserVideoPlayerSettingsCommand>
{
    public UpdateUserVideoPlayerSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
