namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.DeleteUserTrackSelectionPreferences;

public class DeleteUserTrackSelectionPreferencesCommandValidator : AbstractValidator<DeleteUserTrackSelectionPreferencesCommand>
{
    public DeleteUserTrackSelectionPreferencesCommandValidator()
    {
        RuleFor(x => x.LibraryId).NotEqual(Guid.Empty).When(x => x.LibraryId.HasValue);
    }
}
