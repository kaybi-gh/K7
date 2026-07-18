namespace K7.Server.Application.Features.TranscodeSettings.Commands.UpdateTranscodeSettings;

public class UpdateTranscodeSettingsCommandValidator : AbstractValidator<UpdateTranscodeSettingsCommand>
{
    public UpdateTranscodeSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
