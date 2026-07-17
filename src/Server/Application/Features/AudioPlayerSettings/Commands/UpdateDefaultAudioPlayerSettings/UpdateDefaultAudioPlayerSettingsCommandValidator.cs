namespace K7.Server.Application.Features.AudioPlayerSettings.Commands.UpdateDefaultAudioPlayerSettings;

public class UpdateDefaultAudioPlayerSettingsCommandValidator : AbstractValidator<UpdateDefaultAudioPlayerSettingsCommand>
{
    public UpdateDefaultAudioPlayerSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
