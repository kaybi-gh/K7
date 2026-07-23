namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateDefaultVideoPlaybackPolicySettings;

public class UpdateDefaultVideoPlaybackPolicySettingsCommandValidator : AbstractValidator<UpdateDefaultVideoPlaybackPolicySettingsCommand>
{
    public UpdateDefaultVideoPlaybackPolicySettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
