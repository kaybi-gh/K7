namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.UpdateUserTrackSelectionPreferences;

public class UpdateUserTrackSelectionPreferencesCommandValidator : AbstractValidator<UpdateUserTrackSelectionPreferencesCommand>
{
    public UpdateUserTrackSelectionPreferencesCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.LibraryId).NotEqual(Guid.Empty).When(x => x.LibraryId.HasValue);
    }
}
