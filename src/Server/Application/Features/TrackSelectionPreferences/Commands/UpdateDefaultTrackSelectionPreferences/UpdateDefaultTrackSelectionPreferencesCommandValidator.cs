namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.UpdateDefaultTrackSelectionPreferences;

public class UpdateDefaultTrackSelectionPreferencesCommandValidator : AbstractValidator<UpdateDefaultTrackSelectionPreferencesCommand>
{
    public UpdateDefaultTrackSelectionPreferencesCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.LibraryId).NotEqual(Guid.Empty).When(x => x.LibraryId.HasValue);
    }
}
