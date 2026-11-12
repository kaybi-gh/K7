namespace K7.Server.Application.Features.VideoPlayerSettings.Commands.UpdateUserVideoPlayerSettings;

public class UpdateUserVideoPlayerSettingsCommandValidator : AbstractValidator<UpdateUserVideoPlayerSettingsCommand>
{
    public UpdateUserVideoPlayerSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.Settings.NextEpisodeBehavior)
            .IsInEnum()
            .When(x => x.Settings is not null);
        RuleFor(x => x.Settings.InactivityPromptAfterEpisodes)
            .InclusiveBetween(0, 10)
            .When(x => x.Settings is not null);
    }
}
