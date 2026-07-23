namespace K7.Server.Application.Features.MusicIntelligence.Commands.UpdateMusicIntelligenceSettings;

public class UpdateMusicIntelligenceSettingsCommandValidator : AbstractValidator<UpdateMusicIntelligenceSettingsCommand>
{
    public UpdateMusicIntelligenceSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
