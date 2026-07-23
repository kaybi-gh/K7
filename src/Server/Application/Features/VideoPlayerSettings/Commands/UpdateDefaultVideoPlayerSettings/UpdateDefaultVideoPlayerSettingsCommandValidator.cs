namespace K7.Server.Application.Features.VideoPlayerSettings.Commands.UpdateDefaultVideoPlayerSettings;

public class UpdateDefaultVideoPlayerSettingsCommandValidator : AbstractValidator<UpdateDefaultVideoPlayerSettingsCommand>
{
    public UpdateDefaultVideoPlayerSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
