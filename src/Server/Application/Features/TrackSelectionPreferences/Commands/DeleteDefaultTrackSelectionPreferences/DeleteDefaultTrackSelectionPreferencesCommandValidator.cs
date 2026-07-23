namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.DeleteDefaultTrackSelectionPreferences;

public class DeleteDefaultTrackSelectionPreferencesCommandValidator : AbstractValidator<DeleteDefaultTrackSelectionPreferencesCommand>
{
    public DeleteDefaultTrackSelectionPreferencesCommandValidator()
    {
        RuleFor(x => x.LibraryId).NotEqual(Guid.Empty).When(x => x.LibraryId.HasValue);
    }
}
