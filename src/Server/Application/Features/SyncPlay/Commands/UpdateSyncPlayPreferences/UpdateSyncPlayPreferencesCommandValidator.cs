namespace K7.Server.Application.Features.SyncPlay.Commands.UpdateSyncPlayPreferences;

public class UpdateSyncPlayPreferencesCommandValidator : AbstractValidator<UpdateSyncPlayPreferencesCommand>
{
    public UpdateSyncPlayPreferencesCommandValidator()
    {
        RuleFor(x => x.Preferences).NotNull();
    }
}
