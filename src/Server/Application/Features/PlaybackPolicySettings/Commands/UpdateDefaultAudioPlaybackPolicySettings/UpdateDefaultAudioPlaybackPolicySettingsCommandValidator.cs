namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateDefaultAudioPlaybackPolicySettings;

public class UpdateDefaultAudioPlaybackPolicySettingsCommandValidator : AbstractValidator<UpdateDefaultAudioPlaybackPolicySettingsCommand>
{
    public UpdateDefaultAudioPlaybackPolicySettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
