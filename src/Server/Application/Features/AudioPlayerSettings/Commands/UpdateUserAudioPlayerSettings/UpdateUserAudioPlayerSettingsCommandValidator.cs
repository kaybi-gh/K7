namespace K7.Server.Application.Features.AudioPlayerSettings.Commands.UpdateUserAudioPlayerSettings;

public class UpdateUserAudioPlayerSettingsCommandValidator : AbstractValidator<UpdateUserAudioPlayerSettingsCommand>
{
    public UpdateUserAudioPlayerSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
