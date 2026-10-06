namespace K7.Server.Application.Features.LastVideoTrackSelection.Commands.UpsertLastVideoTrackSelection;

public class UpsertLastVideoTrackSelectionCommandValidator : AbstractValidator<UpsertLastVideoTrackSelectionCommand>
{
    public UpsertLastVideoTrackSelectionCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.Selection).NotNull();
        RuleFor(x => x.Selection.AudioLanguage).MaximumLength(16).When(x => x.Selection.AudioLanguage is not null);
        RuleFor(x => x.Selection.SubtitleLanguage).MaximumLength(16).When(x => x.Selection.SubtitleLanguage is not null);
    }
}
