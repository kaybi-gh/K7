namespace K7.Server.Application.Features.GeneralPreferences.Commands.UpdateUserGeneralPreferences;

public class UpdateUserGeneralPreferencesCommandValidator : AbstractValidator<UpdateUserGeneralPreferencesCommand>
{
    public UpdateUserGeneralPreferencesCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
    }
}
