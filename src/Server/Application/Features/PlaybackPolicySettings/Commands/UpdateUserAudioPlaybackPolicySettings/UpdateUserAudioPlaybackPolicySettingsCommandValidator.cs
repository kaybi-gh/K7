namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateUserAudioPlaybackPolicySettings;

public class UpdateUserAudioPlaybackPolicySettingsCommandValidator : AbstractValidator<UpdateUserAudioPlaybackPolicySettingsCommand>
{
    public UpdateUserAudioPlaybackPolicySettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
