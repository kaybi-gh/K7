namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateUserVideoPlaybackPolicySettings;

public class UpdateUserVideoPlaybackPolicySettingsCommandValidator : AbstractValidator<UpdateUserVideoPlaybackPolicySettingsCommand>
{
    public UpdateUserVideoPlaybackPolicySettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
