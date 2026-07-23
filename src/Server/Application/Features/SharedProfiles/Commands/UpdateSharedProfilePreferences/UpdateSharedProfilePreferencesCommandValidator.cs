namespace K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfilePreferences;

public class UpdateSharedProfilePreferencesCommandValidator : AbstractValidator<UpdateSharedProfilePreferencesCommand>
{
    public UpdateSharedProfilePreferencesCommandValidator()
    {
        RuleFor(x => x.Preferences).NotNull();
    }
}
